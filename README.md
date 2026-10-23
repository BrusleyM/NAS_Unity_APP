# NEO AR Showroom

NEO AR Showroom is a Unity mobile application for exploring a dealership's
vehicles in augmented reality. Customers can browse a vehicle catalogue, build
an estimate, place a selected vehicle in AR, and ultimately save configurations
or submit a lead to a dealership.

The companion API lives in the sibling `NAS_Backend` project. It is an ASP.NET
Core 10 Web API backed by PostgreSQL; it owns customer accounts, dealership
data, vehicle data, saved configurations, and leads.

## Current capabilities

- Browse locally authored vehicle catalogue entries.
- Filter and search vehicles, select one, and begin the AR flow.
- Place and interact with vehicles using Unity AR Foundation (ARCore on Android,
  ARKit on iOS).
- Calculate affordability estimates locally.
- Register and log in against the real backend: `AuthController` validates
  input locally first, then calls `ICustomerAuthApi`/`CustomerAuthApi`, which
  POSTs to `api/customer/auth/login` and `api/customer/auth/register` via
  `ApiClient.PostJson` (a real `UnityWebRequest` call, not a stub), and
  publishes `AuthSucceededEvent`/`AuthFailedEvent` on the result.

## Architecture

The Unity app keeps presentation, flow, and reusable domain code separate:

```
Assets/Scripts/
  Core/           App state, domain models, events, AR flow and business services
  UI Docs/        UI Toolkit screen controllers and reusable UI components
  Storage/        Asset storage abstractions and S3-backed implementations
  Configurations/ Environment-specific storage configuration
  AR Scripts/     Input and AR raycast/placement adapters
```

UI controllers publish intent events such as `LoginRequestedEvent`,
`CarSelectedEvent`, and `EstimateSubmittedEvent`. Controllers such as
`AuthController` handle those events and publish success or failure events;
`GameManager` observes the resulting state changes. This keeps the UI independent
of API, AR, and storage implementation details.

Vehicle catalogue items are `CarData` ScriptableObjects stored in
`Assets/Resources/Cars`. They are loaded by the catalogue screen at runtime.

## Backend API integration status

The client boundary is a small API layer under `Core`, not direct HTTP calls
from UI controllers — `Assets/Scripts/Core/Networking/ApiClient.cs` wraps
`UnityWebRequest` (`ApiClient.PostJson<TRequest, TResponse>`), and
`Assets/Scripts/Core/Auth/` builds on it for auth specifically.

**Done:**

- Customer registration and login call the real backend —
  `ICustomerAuthApi`/`CustomerAuthApi` POST to `api/customer/auth/register`
  and `api/customer/auth/login` against `ApiSettings.BaseUrl` (defaults to
  `http://localhost:5080`), and `AuthController` maps the result into
  `AuthSucceededEvent`/`AuthFailedEvent`.
- The sign-in stays saved across launches (`TokenStorage`/`AuthSession`,
  `Assets/Scripts/Core/Auth/`, in PlayerPrefs for now). Login also returns a
  30-day refresh token. At launch `AuthController` swaps it for a new access
  token and goes straight to dealership selection (the splash waits for it); while
  signed in it swaps again five minutes before the hour-long access token lapses
  (`SessionRefreshPolicy`), and when the server rejects a token. Only the
  server refusing the refresh token (401) signs the customer out; no connection
  keeps the saved login. Sign Out revokes it on the server.

**Not done yet:**

- No feature service attaches `Authorization: Bearer <token>` anywhere in the
  codebase, and there's no 401-handling (clear session, return to login) —
  because auth is currently the *only* API surface the client calls; nothing
  else needs the token yet.
- The vehicle catalogue is still local-only:
  `CarSelectionScreenController.InitializeCarData()`
  (`Assets/Scripts/UI Docs/Controllers/CarSelectionScreenController.cs`) loads
  `CarData` via `Resources.LoadAll<CarData>("Cars")`, not an API call. See
  this project's `.claude/CLAUDE.md` for the planned `IVehicleCatalogApi` seam.
- There's no `ILeadApi` or saved-configuration API client — leads and saved
  configurations aren't implemented on the Unity side at all yet.

The backend also has a separate invite-only staff authentication flow for
dealership dashboards (`api/staff/auth/*`). That staff flow should not be part
of the customer-facing AR app unless a future staff/kiosk mode explicitly
requires it.

## Prerequisites

- Unity **6.3** (the project currently uses `6000.3.9f1`)
- Android Build Support for Android development; an ARCore-compatible device for
  on-device AR tests
- iOS Build Support and an ARKit-compatible device for iOS development
- The `NAS_Backend` project and PostgreSQL when testing real API integration

## Getting started

1. Open this folder in Unity Hub using the supported Unity version.
2. Allow Unity to resolve packages and import assets.
3. Open the main scene and run it in the Editor for UI flow work.
4. Switch the build target to Android or iOS to test AR on a supported device.
5. To exercise login/registration against a real backend, run the sibling
   `NAS_Backend` project locally and point `ApiSettings.BaseUrl` at it (default
   is already `http://localhost:5080`, matching the backend's own default).

## Optional: HTTPS for testing on a physical device

Not required for Editor work or normal API integration — the backend's plain
`http://localhost:5080` (see `NAS_Backend`'s own README) is enough for that.
If you're testing on a real Android/iOS device and it rejects the backend's
self-signed HTTPS dev certificate, `NAS_Backend`'s `nginx/README.md` has an
optional local proxy that terminates HTTPS with a certificate the device can
be made to trust, instead of disabling certificate validation in the client.

**Switching the app to use it:** this is a project-wide setting, not
something buried in a UI controller. `GameManager` (on the persistent
`GameManager` GameObject, `DontDestroyOnLoad`) has an `Environment` field:

- `AppEnvironment.Local` — the default. `http://localhost:5080`. Always
  safe, always works in the editor; this is what's used normally.
- `AppEnvironment.ApiDomain` — `https://api.nas.test:8443`, i.e. the nginx
  proxy above. Only works on a machine that's actually run the
  `nginx`/`mkcert`/`/etc/hosts` setup in `NAS_Backend/nginx/`.
- `AppEnvironment.ApiIp` — the same proxy by this Mac's LAN IP, for a phone
  on a network that can't resolve `api.nas.test`. Edit
  `Assets/Resources/Config/ApiSettings.ApiIp.asset` when the IP changes.
- `AppEnvironment.Staging` — the hosted staging backend. A release build
  always uses this, whatever the dropdown says.

Each one's address is `Assets/Resources/Config/ApiSettings.<Name>.asset`,
loaded by `EnvironmentResolver`; the same environment value also decides
whether to skip TLS certificate validation (see below), so there's exactly
one switch to flip, not several that have to be kept in sync by hand.
**The committed setting is `Staging`** (the Game Manager prefab). To test against your own
machine pick `Local`, `ApiDomain` or `ApiIp` in the editor and do not commit that change.
`EnvironmentResolver` logs which one is active
(`[Environment] Staging -> https://nas-api.neoxr.co.za`) so you can confirm which mode you're in.

*(This used to be a bool living directly on `AuthController`, with a
separate bool on the `ApiSettings` asset controlling certificate bypass.
Both were removed — the first because a per-script toggle isn't really
"project-wide," the second because it could be, and once was, toggled off by
itself without anyone touching the thing that was supposed to control it,
silently reintroducing a bug that had already been fixed.)*

**Certificate gotcha, already handled — but worth knowing about:**
`UnityWebRequest` validates TLS certificates against UnityTls's own bundled
CA list, not the OS/Keychain trust store. That means `mkcert -install`
(which only updates the OS trust store — enough for curl/Safari) has **no
effect** on Unity's own HTTP client: even with everything else set up
correctly, you'd still hit `Curl error 60: Cert verify failed... UnityTls
error code: 7`. When the environment is `ApiDomain`, `ApiClient` attaches
`AcceptAllCertificatesHandler` (`Core/Networking/`) to skip that validation —
driven by the same environment value above, so normal/production traffic
(`Local`) always gets real certificate validation. Verified end-to-end in
Play mode: a login attempt through `https://api.nas.test:8443` reaches the
backend and gets a real `401` response instead of failing on the TLS
handshake.

## Key packages

- Unity AR Foundation, ARCore, and ARKit
- Unity Input System and UI Toolkit
- Universal Render Pipeline
- AWS SDK-backed storage services for model assets

## Related project

The backend source is available at:

https://github.com/BrusleyM/NEO_AR_Showroom_Backend.git

Its README describes the PostgreSQL setup, migrations, and available API
endpoints.
