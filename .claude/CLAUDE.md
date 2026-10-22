# NAS Showroom — Unity AR car-showroom app

Unity project. UI is built with UI Toolkit (UXML/USS), not uGUI. Source lives under
`Assets/Scripts/`, UI documents under `Assets/UI Toolkit/UI Docs/`.

## Architecture

**Event-driven, not direct references.** Controllers never hold references to each
other. Everything communicates through a static, type-safe pub/sub bus:

- `Assets/Scripts/Core/Events/EventBus.cs` — `EventBus.Subscribe<T>/Unsubscribe<T>/Publish<T>`
- `Assets/Scripts/Core/Events/GameEvents.cs` — all event payload structs
  (`LoginRequestedEvent`, `AuthSucceededEvent`, `AuthFailedEvent`, `CarSelectedEvent`, etc.)

Always pair `Subscribe` in `OnEnable` with `Unsubscribe` in `OnDisable`, or destroyed
MonoBehaviours keep receiving events.

**TODO, not urgent — known deviation from the above:** `GameManager.Instance`
is already referenced directly (not via events) by several scripts —
`ParentPageController`, `EstimatorCardController`, `AuthController` (reads
`CurrentEnvironment`), and a couple of storage test scripts. This predates
the event-driven principle and keeps being extended rather than fixed.
Moving all of these to go through `EventBus` instead is a real future goal,
but should happen as one deliberate, dedicated pass across every call site —
not piecemeal, one at a time, whenever a new feature happens to touch
`GameManager`.

**This is a deliberate hybrid, not an oversight — keep it that way.**
`GameManager` is the sole direct subscriber to the raw domain events that
feed its session state (`AuthSucceededEvent`, `CarSelectedEvent`) — nothing
else subscribes to those two directly. After updating its own
`CurrentUser`/`AccessToken`/`SelectedCar`, it republishes its own
`SessionAuthenticatedEvent`/`SessionCarSelectedEvent`, and everything else
(`ParentPageController`, and any future screen that needs to react to auth or
car selection) subscribes to THOSE instead. This is what makes it safe to
read `GameManager.Instance` state from inside one of those handlers: since
`GameManager` only publishes the `Session*Event` after its own field is
already set, it's structurally impossible for a subscriber to observe it
early, regardless of subscription order. Moving to "every consumer
subscribes to the raw event itself, nothing reads `Instance`" was considered
and rejected — it doesn't remove any real risk beyond what the republish
pattern already gives, and forces every future screen needing session state
to re-subscribe to auth/car-selection events itself, real ongoing overhead
for no upside.

This exists because of a real bug: `CarSelectionScreenController` read
`AccessToken` from within `AuthSucceededEvent`'s own handling chain (via
`ParentPageController.OnAuthSucceeded` → `ShowCarSelectionScreen()` →
`AddComponent<CarSelectionScreenController>()` → `OnEnable()`), before
`GameManager.OnAuthSucceeded` had set it — a null token went out on the
vehicle-catalog request, backend correctly rejected it as unauthorized. First
fix attempt was moving `GameManager`'s event subscriptions from `OnEnable()`
to `Awake()` (Unity guarantees every object's `Awake()` finishes before any
object's `OnEnable()` runs within the same load, so this made `GameManager`'s
handler win the race) — that's still in place and still correct practice,
kept as defense in depth, but it's no longer the thing actually preventing
this bug. The `Session*Event` republish pattern above is the actual structural fix: it doesn't depend
on execution-order trivia at all, so it can't quietly break if someone adds a
new event without knowing to apply the `Awake()` rule to it.

**The rule going forward**: if a new event needs GameManager to update its
own state AND other scripts need to react with that state already
guaranteed-current, add a `Session*Event` GameManager publishes after the
update, same as the two above — don't have consumers subscribe to the raw
event and read `GameManager.Instance` inside the handler. `ReturnToEstimatorRequestedEvent`
doesn't have a `Session*Event` counterpart yet because nothing currently
reacts to it synchronously (it's checked later via the `ReturnToEstimator`
flag in `DecideInitialScreen()`) — add one if that ever changes, don't add it
speculatively now.

The remaining direct-`Instance` reads named above (`EstimatorCardController`,
`AuthController`'s `CurrentEnvironment`, the storage test scripts) aren't
reading data derived from a same-frame event the way the fixed bug was —
they're a separate, milder concern (coupling/testability), still worth the
dedicated pass mentioned above eventually, just not urgent or bug-causing.

**Screen flow (current order):** Auth → dealership selection → car selection →
AR placement → affordability calculator/estimator. `ParentPageController` is a pure
router — it holds `VisualTreeAsset` references for each screen (assigned in
the Inspector) and swaps `_cardContainer`'s content in response to events. It
does not know how login/register/auth work, only which screen to show next.
Card controllers (`LoginCardController`, `RegisterCardController`,
`CarSelectionScreenController`, `EstimatorCardController`) are added
dynamically via `gameObject.AddComponent<T>()` — they are never pre-placed
in the scene, so they have no Inspector to drag references into.

**Important gotcha:** `AddComponent<T>()` runs `OnEnable` synchronously before the
calling code gets a chance to hand anything to the new component. Any controller that
needs data from its creator (e.g. `CarSelectionScreenController` needs a
`VisualTreeAsset` for the card template) uses an explicit `Initialize(...)` method
called right after `AddComponent`, with `OnEnable` doing only DOM-querying/event
wiring that doesn't depend on that data.

## Dealership selection (before car selection)

The customer picks where they're buying from (QR-code scanning is a possible v2).
`DealershipSelectionController` (UXML in `Assets/Resources/UI/DealershipSelection.uxml`,
loaded by `ParentPageController` via `Resources.Load` so no scene edit was needed)
lists `GET /api/customer/dealerships` (only dealerships with active vehicles) and
publishes `DealershipSelectedEvent`. Same raw/`Session*Event` split as car
selection: `GameManager` is the only raw subscriber, sets `SelectedDealership`
(cleared on every login, and clears `SelectedCar`/`SelectedConfigurationId` if the
dealership changed), republishes `SessionDealershipSelectedEvent`, and tells the
backend via `POST /api/telemetry/session/dealership` (deferred until
`TelemetrySessionId` exists). The car list is fetched with that dealership's id, so
a lead (which takes its dealership from the vehicle) always lands with the chosen
one; the estimator/test-drive requests also send `customerSessionId` so the backend
rejects a vehicle from a different dealership (409). The last choice is only
remembered (PlayerPrefs) to highlight it, never auto-selected. The car selection
screen shows the chosen dealership with a "Change" control
(`ChangeDealershipRequestedEvent`).

## Side menu and profile screens (Figma update of 11 Oct 2026)

Car Selection's hamburger button publishes `MenuRequestedEvent`; `ParentPageController` overlays
`Resources/UI/HamburgerDrawer.uxml` on the document root (it needs no scene edit - the menu and its three screens
load from `Resources/UI/`, like the dealership list). The menu shows the signed-in customer and the chosen dealership
(with "Change", which reuses `ChangeDealershipRequestedEvent`) and announces Edit Profile / Change Password / Privacy /
Sign Out as events. The three screens are ordinary cards (`EditProfile`, `ChangePassword`, `Privacy` UXML plus matching
controllers); finishing one publishes `CloseProfileScreenRequestedEvent`, which returns to Car Selection.

- Backend: `GET/PUT /api/customer/profile` and `POST /api/customer/auth/change-password`
  (NAS_Backend `CustomerProfileController` / `CustomerAuthController`). The email is read-only.
  Changing a password needs the current one; existing tokens stay valid until they expire.
- Sign Out: `LogoutRequestedEvent` -> `AuthController` forgets the token, `GameManager` ends the telemetry
  session (the leaving screen's visit is sent first) and clears the session, then publishes `SessionEndedEvent`
  (same "publish after the state is set" pattern as `SessionAuthenticatedEvent`); the router shows Login on that.
- Client-side validation for the profile forms is `Core/ProfileValidation.cs` (pure logic, unit-tested and also
  compiled by `CI/PureLogicTests`), using the same rules as registration and the backend.
- The dealership screen (`Resources/UI/DealershipSelection.uxml`) now selects a card and confirms with a Continue
  button; it is in "change mode" (Back button, "Current" badge, a warning that the selected car is cleared) whenever
  `GameManager.SelectedDealership` is already set, i.e. when reached from the menu rather than straight after login.
- `uxml.Instantiate()` wraps the UXML in a `TemplateContainer`: remove that wrapper, not just the inner element, or
  an empty full-screen element is left over the screen and swallows every tap (this happened with the menu).
- The UXML `is-password` attribute is not reliable; set `isPasswordField = true` in code (as `LoginCardController` does).
- The Privacy screen's text is a plain-language summary of what the app records today, written from the code. It makes no
  promises about retention or deletion - review it before release.

## Expired login (session expiry)

The login token lasts 60 minutes on the server. Every API wrapper goes through `ApiClient`; when a request that carried a
bearer token is answered 401 (`SessionExpiryPolicy.MeansSessionExpired`, pure and unit-tested; a wrong password is a 401 without a
token and a wrong current password is a 400, so neither signs anyone out), `ApiClient.SessionRejected` fires.
`GameManager` listens, and if it still holds a token it publishes `LogoutRequestedEvent` (the normal sign-out pipeline) and then
`SessionExpiredEvent`; the Login screen shows the message. Several requests can be rejected at once: only the first finds a
token to drop. While in AR, `ArViewportController` and `ObjectPlacerController` stop on `SessionEndedEvent` without sending
telemetry, and `ParentPageController` shows its own UI again before showing Login.

## UI motion (`Assets/Scripts/Core/Motion`, assembly `NAS.Core.Motion`)

No third-party tween package. Two layers:

- **USS transitions** (`Resources/UI/Motion.uss`, loaded onto the UI root by `ParentPageController` via `UiMotion.ApplyStyles`): button press
  (`Button:active` scales to 0.965 and springs back), disabled fade, anything that needs no code.
- **`UiMotion`** (UI Toolkit elements) and **`TransformMotion`** (3D objects) for sequenced / data-driven motion: `Enter`, `Stagger`, `Pop`,
  `Shake`, `FadeIn/Out`, `CountUp`, `Spin`, `Run` (custom). Curves are in `Easing` (pure maths, unit-tested; every curve ends exactly on 1).
  Rules every helper follows: the start state is set immediately and the end state applied exactly; one running tween per element and
  channel; leaving the panel cancels an element's tweens; real time, not `Time.timeScale`.

How screens use it: the router (`ParentPageController.SwapCard`, the single place a screen replaces another) calls `UiMotion.Reveal`:
UXML parts tagged with the USS class `motion-item` fade/rise in one after another (stagger capped at about half a second), a screen with none
simply enters as a whole. Add the class to a UXML element to opt it in. Lists built in code stagger themselves (dealership cards); thumbnails
fade in when the real picture arrives; form errors rise in and the field shakes (`FormHelpers`); the Customize sheet slides, the loading
overlay fades, the confirmation check pops, a placed car pops in (`TransformMotion.PopIn`); spinners are `UiMotion.Spin` (one implementation,
it replaced a copy in each of six controllers).

**Reduce motion** is a developer switch only (`NAS > Dev > Reduce UI Motion` in the Editor menu, remembered in PlayerPrefs; `MotionSettings`
ignores it in release builds). It makes every animation jump to its end state, which is what tests and screenshots want. Spinners ignore it.

## Verify email, forgot password and usernames (pre-login screens)

The server (NAS_Backend) emails six-digit codes (10 minutes, 5 wrong guesses, 60 s between sends) and gives every customer a generated
username (`ada4821`). The app side:

- **Sign-in box** takes an email OR a username (an "@" means email; `ProfileValidation.ValidateLoginIdentifier`). The request field is still
  named `email` (`CustomerLoginRequest`), as is the one in every code endpoint, so older servers and apps keep agreeing.
- **Verify email**: when the server wants the address verified (`emailVerificationRequired` on the register answer, or `email_not_verified`
  on a sign-in) `AuthController` publishes `EmailVerificationRequiredEvent`; `ParentPageController` shows `VerifyEmailCardController`
  (`Resources/UI/VerifyEmail.uxml`). A correct code answers with a normal sign-in (token), so it ends in the usual
  `AuthSucceededEvent` -> `SessionAuthenticatedEvent` -> dealership screen. A sign-in that is refused for this sends a fresh code first.
  Registration with no token must never reach `AuthSession.Apply` (it throws on an empty token): `HandleRegisterResult` checks first.
- **Forgot password**: Login card link -> `ForgotPasswordCardController` -> `PasswordResetCodeSentEvent` -> `ResetPasswordCardController`
  (code + new password twice) -> `PasswordResetCompletedEvent` -> login screen with a green notice (`LoginNoticeEvent`, published after the
  card exists). The server answers 200 for any account, so no screen says whether an account exists ("If there is an account for that...").
  "Resend code" on the reset screen raises the same `PasswordResetCodeSentEvent`; the router ignores it while that screen is showing.
- A wrong, expired or used-up code (`code_invalid` / `code_expired` / `too_many_attempts`) is mapped onto the code box by
  `AuthController.PublishCodeFailure`. These calls carry no bearer token, so a 401 on them is never read as an expired session.
- The code box (`CodeCardSupport`): digits only, six at most, numeric keypad; "Resend code" locked with a countdown for 60 s
  (`AccountRecovery.SecondsLeft`, pure and tested).
- **Username**: shown in Edit Profile (`username-group`). Editable while `canChangeUsername` is true; saving sends `PUT /api/customer/profile/username`
  first (refused names show on the username box and nothing else is saved), then the details. After the one change it is read-only.
  `ProfileValidation.ValidateUsername` mirrors the server's shape rule.
- New screens load from `Resources/UI` and share `AccountCards.uss`; screen names `verify_email`, `forgot_password`, `reset_password`.
- Testing without sending mail: run the API locally with no `Email__ApiKey` (the code is written to its console in Development) and, to
  try the registration/sign-in gate, `Auth__RequireEmailVerification=true`. Never point a test at the API that has the real Resend key.

## Telemetry events that feed the ML model

`GameManager` owns sending telemetry events (`LogActivityEvent`, best-effort, skipped until
`TelemetrySessionId` exists). Besides `vehicle_viewed` it records two that the buyer
classifier uses as features (`ar_load_failures`, `dealership_changes` - see `NAS_ML`'s
README, "v4 candidate"):

- `ar_load_failed` - `SelectedCarModelLoader` publishes `ArModelLoadFailedEvent(reason)` at each
  place it falls back to the placeholder (`no_model_key`, `download`, `parse`, `instantiate`).
  A customer who gave up because the car wouldn't load looks very different from one who lost
  interest.
- `dealership_changed` - logged when the customer picks a *different* dealership after already
  choosing one (the first choice isn't a change).

Both send `vehicleModelId = 0` when there is no vehicle (JsonUtility can't send a null int);
the backend stores `<= 0` as null. Until a build with these ships the backend sees 0 for both,
which the model treats as "none reported", not "none happened".

## Screen timing (second counter beside the session timer)

`ParentPageController` publishes `ScreenShownEvent(ScreenNames.X)` whenever a different
screen becomes visible (splash, login, register, dealership_selection, car_selection,
ar_viewport, estimator). `GameManager` feeds them to a `ScreenVisitTracker` (pure logic,
`Assets/Scripts/Core/ScreenVisitTracker.cs`) which sends each finished stretch to
`POST /api/telemetry/screen-visits` when the customer leaves the screen. App pause closes the
current screen (so backgrounded time isn't counted as screen time) and resume reopens it; quit
closes it for good. Visits under 0.5s are dropped. Splash/login/register finish before the
telemetry session exists, so those visits are held (capped at 50) and sent once
`StartTelemetrySession` gets its id.

The session timer is unchanged - the two differ by idle/background time, and visits survive a
force-quit up to the last screen change (the session end call doesn't). The backend also uses
screen visits as one of the sources for "last activity" when a session never got an end time
(`BuyerFeatureBuilder.SessionSeconds`).

New screens: add a `ScreenNames` constant (snake_case, `^[a-z][a-z0-9_]{0,49}$`) and publish
`ScreenShownEvent` where the screen appears. `AR Scene` screens are not separate - the whole AR
scene is "ar_viewport" (its own AR/customise/calculator telemetry already has durations).

The first Unity tests live in `Assets/Tests/EditMode` (`NAS.Tests.EditMode`; run via the Test
Runner's EditMode tab) - add pure-logic tests there.

## Auth (`Assets/Scripts/Core/Auth/`)

`AuthController` is **not** a mock — it's backed by a real API layer
(`ICustomerAuthApi`/`CustomerAuthApi`, `NAS.Core.Networking`, `ApiResult<T>`,
`AuthSession`, `TokenStorage`). It validates client-side first (regex/length checks
matching the Figma-derived React reference in `mobile-designs.tsx`), publishing
per-field errors via `AuthFailedEvent.FieldErrors` (a `Dictionary<string,string>`
keyed by field name: `email`, `password`, `firstName`, `lastName`, `cellNumber`,
`confirmPassword`) *before* ever calling the real API. `AuthFailedEvent.Reason` is
for general/backend-level failures instead (wrong credentials, network error) —
`FieldErrors` is null in that case, `Reason` is null when `FieldErrors` is set.

`LoginCardController`/`RegisterCardController` show these via a `.input-field--error`
class + per-field `Label`s (`email-error-label`, `password-error-label`, etc.) —
shared styling lives in `Assets/UI Toolkit/UI Docs/Styles/LoginScreen.uss`.

**API base URL is a project-wide environment setting, not hardcoded and not
owned by AuthController.** `GameManager` (`Assets/Scripts/Core/GameManager.cs`,
`DontDestroyOnLoad` singleton, `GameManager.Instance`) has
`_environment : AppEnvironment` (`Assets/Scripts/Core/AppEnvironment.cs` —
`Local` or `ApiDomain`), exposed as `CurrentEnvironment`. `AuthController`
reads it in **`Start()`, not `Awake()`** — Unity doesn't guarantee `Awake()`
order across different GameObjects, and `GameManager.Instance` must already
be set; `Start()` is guaranteed to run only after every object's `Awake()`
has — and picks between `_apiSettings` (Local, `http://localhost:5080`) and
`_apiDomainSettings` (ApiDomain, `ApiSettings.ApiDomain.asset`,
`https://api.nas.test:8443` — the optional `NAS_Backend/nginx/` proxy). The
same `CurrentEnvironment` value also decides `trustAnyCertificate`, passed
explicitly through `CustomerAuthApi`'s and `ApiClient`'s constructors (see
the cert gotcha below) — **keep this a single source of truth for both
concerns.** A design with two independently-toggleable flags (one for which
`ApiSettings` to use, one for cert trust) will eventually drift out of sync,
since nothing stops either one being flipped without the other. `Local` is
the simplest default for anyone without the nginx/mkcert setup running;
`ApiDomain` only works on a machine with that setup
(`NAS_Backend/nginx/README.md`) already running — on any other machine it
makes auth fail silently. See the README's "Optional: HTTPS for testing on a
physical device" section for the full explanation.

**Environments.** `AppEnvironment` has four values: `Local`
(`http://localhost:5080`, the editor), `ApiDomain` (`https://api.nas.test:8443`
through the nginx proxy), `ApiIp` (the same proxy by this Mac's LAN IP, for a
phone on a network that cannot resolve the name; edit its asset when the IP
changes) and `Staging` (the SmarterASP.NET site at `https://nas-api.neoxr.co.za`,
real certificate). Each one's address is
`Resources/Config/ApiSettings.<Env>.asset`, loaded by `EnvironmentResolver`
(no Inspector wiring to forget); a missing asset is a loud error, never a silent
fall back. Which one a build **actually uses** is
`EnvironmentPolicy.Effective(...)` (pure logic, EditMode-tested): Editor and
Development builds honour `GameManager._environment`; a **release build
ignores it** and is `Staging`, so a release can never ship pointing at
localhost. `TrustAnyCertificate` is true only for ApiDomain/ApiIp. There is no
Production yet: when it exists, add it as a new enum value (numbers are stored in
the scene and never change), give it its own asset, and return it from
`Effective` for release builds. The full standard is
`NAS_Backend/docs/ENVIRONMENTS.md`.

**Recurring gotcha: adding a cross-folder script reference can compile
"clean" right up until it doesn't.** This project uses one `.asmdef` per
`Core/*` subfolder (`NAS.Core`, `NAS.Core.Auth`, `NAS.Core.Networking`,
etc. — run `find Assets/Scripts -iname "*.asmdef"` to see them all). Adding
a `using` for a type in a different folder is not enough — the *assembly*
also needs to reference the other assembly, or it's a compile error
(`CS0103: The name 'X' does not exist in the current context`), same as any
missing reference. The trap: Unity's incremental compiler can hand back a
transient "0 errors" / successful-reflection-lookup result for a beat after
an edit like this, using a stale cached assembly, before a later trigger
(e.g. entering Play mode, which forces a real check) surfaces the actual
error. **Don't trust an early "looks clean" read as final** after adding a
reference to a type in a different `Core/*` folder — confirm by actually
entering Play mode (which Unity refuses outright if there's a real compile
error) before relying on the change. Fix for a missing reference: add the
target assembly's GUID (from its `.asmdef.meta`) to the source assembly's
`"references"` array in its own `.asmdef`.

## Car selection carousel (`Assets/Scripts/UI Docs/Components/`, controller in `.../Controllers/`)

Shows **one car at a time**, drag/swipe only (no buttons), with object pooling.
`CarPager` and `CarCardView` live in `Assets/Scripts/UI Docs/Components/`;
`CarSelectionScreenController` lives in `Assets/Scripts/UI Docs/Controllers/`
— a different folder, despite all three being part of the same feature. Don't
assume "Components/" for a class just because related classes are there;
verify per-file if it matters for what you're doing.

- `CarPager` — owns a fixed 3-slot pool (`SlotCount = 3`, `CenterSlot = 1`).
  `RefreshCards()` never creates/destroys elements after setup, only rebinds data
  around the current index. `CanGoPrevious`/`CanGoNext` guard the boundaries.
- `CarCardView` — binds one cloned `CarCard.uxml` instance to a `CarData`.
  **Uses `visibility` (not `display`) to hide an empty boundary slot** — `display:none`
  removes the element from flex layout entirely, which breaks the 3-slot symmetry
  `justify-content:center` relies on to keep the current card centered.
- `CarSelectionScreenController` (`Controllers/`) drives the drag: `PointerDown` (capture pointer) →
  `PointerMove` (clamped `translate` on `_carsContainer`, follows the finger 1:1) →
  `PointerUp` (25%-of-viewport-width threshold decides commit vs. snap back). The strip is then eased home by `SettleCarousel` (a committed swipe or a tap on a side card starts one slot over and eases in; a swipe that falls short springs back) - see "UI motion" below.
- `CarData` (`Assets/Scripts/Core/Models/CarData.cs`) is a `ScriptableObject`, but
  there are no more hand-authored `.asset` fixtures — `Assets/Resources/Cars/` is
  gone. Every `CarData` instance is created at runtime by
  `VehicleDtoMapper.ToCarData()` (`Core/Vehicles/VehicleDtoMapper.cs`) from the
  customer vehicle API response. `CarPager`, `CarCardView`, and
  `CarSelectionScreenController` only ever interact with the `CarData` *type* —
  they don't know or care that it's API-sourced, which is what made this migration
  low-risk when it happened.

## Vehicle catalog: real 3D models, not just names (`Assets/Models/cars/`)

The catalog is 9 vehicles named `<Make/Model> DEMO` (e.g. `BMW M8 DEMO`) — the
`DEMO` suffix is deliberate on both the DB row and the Tigris object key, so
nothing in the shipped app reads as an actual partnership with the real
manufacturers. Backing 3D models are plain `.glb` files in `Assets/Models/cars/`
(glTFast imports) — **there is no wrapper prefab layer**. The files in that
folder are the corrected, ready-to-use source of truth; place/instantiate them
directly.

**Why "corrected" matters — two real bugs found in the original downloads,**
fixed at the source (not worked around with a Unity-side wrapper):
1. **Scale.** Several of these Sketchfab-sourced `.glb` files imported at wildly
   wrong scale (some ~100x too small, some ~5-10x too big) — glTF is spec'd in
   meters so this was baked into the source file, not a Unity import setting.
2. **Orientation and pivot.** All 11 originally imported standing on their nose
   with their pivot off-center (a couple by several meters on X/Z, one with its
   lowest point 0.6m below ground).

Both were fixed by running each model through Blender (`brew install --cask
blender`, driven headlessly via `blender --background --python script.py`):
join all mesh parts into one object, neutralize whatever transform the source
file's node hierarchy carried (**must use the object's full evaluated
`matrix_world`, not just `matrix_basis`** — some of these files parent meshes
through several levels of bone/empty nodes, e.g. the Tesla model, each level
carrying its own rotation/scale that `matrix_basis` alone silently ignores),
apply a uniform scale correction computed against the vehicle's real-world
length, recenter the pivot to the ground-touching centroid, and re-export.
**No rotation correction is needed** — once neutralization correctly accounts
for the full parent chain, Blender's own glTF exporter handles the Z-up →
Y-up axis conversion correctly on its own; manually adding a corrective
rotation on top (which earlier, incomplete versions of this pipeline needed)
becomes actively wrong and reintroduces the "standing on its nose" bug.
Verify any reprocessed model the same way this batch was verified: reimport
into Unity, check combined `Renderer.bounds` against the vehicle's real-world
L×W×H, confirm `bounds.min.y ≈ 0` and `bounds.center.x/z ≈ 0`, then eyeball a
screenshot — bounds alone can't be fully trusted (an unrelated stray mesh once
inflated one model's naive combined bounds to 10m; a median-filtered
per-submesh check is what caught it).

**Also fixed by the same pass:** `bpy.ops.object.transform_apply()` and
`bpy.ops.object.origin_set()` both proved unreliable in headless/background
mode for a handful of these specific files — silently no-op'ing (identical
output on retry) or producing a double-scale bug when combined with a second
correction step. The working script bakes transforms via direct
`Mesh.transform(matrix)` / vertex manipulation instead of those operators,
which has no operator/context/cache state to misbehave.

**Now wired up:** each vehicle's corrected `.glb` is uploaded to Tigris under
`dealerships/{dealershipId}/vehicles/{vehicleId}/model.glb` (older notes below say `carmodels/<key>.glb`, the layout before 11 Oct 2026), the DB's
`vehicle_model.tigris_model_key` column records which object belongs to which
row, and `SelectedCarModelLoader`
(`Assets/Scripts/Core/SelectedCarModelLoader.cs`, on the "ar man" GameObject
in `AR Scene.unity`, alongside `ObjectPlacerController`) downloads the
selected vehicle's model bytes at `Start()` and loads them via glTFast's
**runtime** API (`GltfImport.LoadGltfBinary` +
`InstantiateMainSceneAsync`, distinct from the Editor-time import path
glTFast also provides) rather than AssetBundles — glTF is a portable format
glTFast can parse identically on any platform, so this avoids AssetBundles'
per-`BuildTarget` build/upload duplication. The instantiated model is parked
at `y=-1000` and handed to `IARPlacementService.RaycastPrefab`, which
`ObjectPlacerController` then instantiates at the AR tap point — replacing
the old fixed `Cube.prefab` placeholder. Any failure along the way (no
`tigrisModelKey` on the selected car, download error, glTF parse/instantiate
error) falls back to whatever placeholder prefab was already assigned, never
blocking placement outright. `ObjectPlacerController` no longer auto-starts
placement in `Start()` — `SelectedCarModelLoader` calls `EnablePlacement()`
once it knows what to place, so a tap can't land before the async swap
finishes.

**TODO, not urgent — no clean retry after a failed model load.** When
`LoadSelectedCarAsync()` hits any of the failure points above, it falls back
to the placeholder prefab (currently still the old `Cube`, confirmed still
assigned on `XR Origin`'s `ARRaycastManager.raycastPrefab`) for the rest of
that AR session — there's no way to retry loading the actual selected car's
model without leaving `AR Scene` and re-entering (which re-fires
`EnterArRequestedEvent` and re-runs `LoadSelectedCarAsync()` from scratch).
Deliberately not building this now — needs a real decision on UX first (a
retry button on some kind of error state? auto-retry with backoff? how many
attempts before giving up and just committing to the cube?), not just a
code change. Revisit once that's decided; when it lands, make sure the retry
path also cleans up `_currentModelRoot` the same way `LoadSelectedCarAsync()`
already does at its top, so a retry after a partial `InstantiateMainSceneAsync`
failure doesn't leak.

## AR viewport screen

Design source: `ARViewportScreen` in `mobile-designs.tsx` (the code-bundle export
lives outside this repo, under `~/Downloads/NAS Showroom/src/app/components/` when
last checked — re-locate it via that folder name if it's moved).

**Important architectural correction vs. what this doc used to say:** the AR
viewport is a **real separate Unity scene** (`Assets/Scenes/AR Scene.unity`, with
its own `AR Session`/`XR Origin`/AR Foundation setup, already in
`ProjectSettings/EditorBuildSettings.asset`), not another UI Toolkit card added
dynamically via `AddComponent<T>()` into `ParentPageController`'s `_cardContainer`
the way Login/CarSelection/Estimator are. `Main App.unity` is also now registered
in Build Settings (it wasn't before this was built — required for
`SceneManager.LoadScene(string)` to resolve it by name).

Screen flow, as actually wired:
- `CarSelectionScreenController.OnStartARClicked` publishes `CarSelectedEvent`
  (`GameManager.SelectedCar` updates from it, including `VehicleInfo.id`, which
  survives the scene load since `GameManager` is `DontDestroyOnLoad`).
- `ParentPageController.OnCarSelected` calls `SceneManager.LoadScene("AR Scene")`
  — no card, no `ShowArViewportCard()`.
- `AR Scene.unity` has a pre-placed `AR UI` GameObject (`UIDocument` + a
  scene-placed, **not** `AddComponent`-dynamic, `ArViewportController`) —
  `Assets/UI Toolkit/UI Docs/ArViewportScreen.uxml` /
  `Styles/ArViewportScreen.uss`. `ArViewportController.OnEnable` reads
  `GameManager.Instance.SelectedCar.modelName` straight into the car-name label.
- **Back button** → `SceneManager.LoadScene("Main App")` with no event published.
  Back in `Main App.unity`, `ParentPageController.DecideInitialScreen()` sees
  `CurrentUser`+`SelectedCar` already set and `ReturnToEstimator` false, so it
  calls `ShowCarSelectionScreen()` — which restores the previously selected car's
  position via `CarSelectionScreenController`'s index-restore logic (matches
  `_filteredCars` against `GameManager.SelectedCar.id`, falls back to index 0 if
  not found).
- **Confirm (checkmark) button** → publishes `ReturnToEstimatorRequestedEvent`
  (the same event `GameManager` already listened for, previously only used when
  backing out of an in-progress estimate) then loads `Main App`.
  `DecideInitialScreen()` sees `ReturnToEstimator` true and calls
  `ShowEstimatorCard()` instead, resetting the flag.

**Built so far:** top bar (Back button, centered car name label, circular cyan
Confirm/checkmark button), a bottom-center Settings button that opens a
"Customize" bottom sheet (drag handle, header with close button, the category
buttons below — tapping the backdrop or the close button closes it), and
real manipulation of the placed car — one-finger drag to reposition, two-finger
pinch to scale (clamped 1x–2.5x real-world size, never smaller — shrinking below
1:1 defeats the point of seeing it at true scale), and a rotation slider (`-180°`
to `180°`, positioned above the Settings button) instead of the two-finger twist
gesture originally planned — real users found twisting hard to do accurately.
`CarManipulationController` (`Assets/Scripts/Core/CarManipulationController.cs`,
on the "AR Man" prefab alongside `ObjectPlacerController`/`CarPaintController`)
owns all three; `ArViewportController` owns the slider control and the
"Pinch to scale `[1:x]`" hint label (bottom-left, live-updating — the ratio is
real-world size : current displayed size, e.g. `[1:1.5]` at 1.5x), purely
event-driven (`RotationSliderChangedEvent`/`CarScaleChangedEvent`/
`GestureCountsUpdatedEvent`) in both directions, same as paint colour. Real
interaction counts feed the `ArSession` telemetry `ObjectPlacerController`
already sent — see the "ML buyer classification" work in `NAS_Backend`'s own
CLAUDE.md-equivalent docs for where `ar_rotations`/`ar_repositions`/`ar_scales`
were consumed as model features before any real gesture system existed to
produce them.
Colors match the Figma spec (`#C0C0C0` neutral, `#00D4FF` active/accent) but
`box-shadow` glow wasn't reproduced (UI Toolkit's USS support for it wasn't used
here — check current Unity version support before assuming it's unavailable).

A **"Request test drive"** pill sits next to the Settings button once a car is
placed (`ArViewportController.OnTestDriveClicked`). It calls
`POST /api/estimator/test-drive-requests`, which records a `test_drive_requested`
activity event and gives the customer's lead `TestDriveRequestedAt` (creating a
lead from their account if they haven't submitted the estimator yet — the
estimator's submit then completes that same lead). A lead with a test drive
requested AND the form submitted is classified Hot with confidence floored at
0.90 by the backend (a hand-set rule in `MlDataService`, to be replaced once the
model is retrained on real outcomes), and NAS-Admin explains why. This is the
only real source of the `test_drive_requested` signal the ML training data
counts.

**Customize sheet** (four buttons; Dashboard was dropped). **Paint** swaps the body colour (the car's catalogue colours,
`CarData.exteriorColors`, saved with the build). **Wheel** and **Interior** switch between the model's alternative meshes
(`Wheels_A`/`Wheels_B`, `Interior_A`/`Interior_B` with the door cards): `CarVariantSwitcher` on the placed car shows the chosen
letter and hides the rest (`ModelVariantRules`), the buttons show one chip per letter the car has, coloured by its rim or leather.
**Trims** swap the `Trim*` parts to the `NAS/TrimGlow` shader (`Resources/Shaders/TrimGlow.shader`, loaded by `TrimGlow`): steady,
pulsing or flowing glow, presets in `TrimStyles` (first one, Original, puts the car's own trim back). `CarTrimController` on the placed
car applies them. Models built before the A/B pass (the nine older demo cars) have no variants, so those buttons say the part can't be
changed on this car. Prices: the customer API returns each car's `wheels`, `interiors` and `trimLevels` (with `priceAdjustment`) next to
`exteriorColors`; the app matches wheel/interior chips to option rows by the style letter their name ends in (`Style B`) and trim chips to
trim levels by the glow style's name (`OptionMatch`), and shows a "+R9 000" tag under any chip that costs extra. The whole choice lives in
`GameManager.Customization` (`CustomizationSelection`, cleared per car and per AR visit), is sent on Confirm as `exteriorColorOptionId`,
`wheelOptionId`, `interiorOptionId`, `trimLevelId` (0 = not chosen) and shows in the dashboard's Builds tab; the Estimator's price is the
base price plus `Customization.OptionsTotal`. Only trim styles count toward `colourChangeCount`.
Still not built: the camera-feed gradient scrim.

**TODO, not urgent — telemetry-sending isn't consistently factored out of the
controller that owns the interaction.** `ObjectPlacerController` still builds
and sends `ArSession` telemetry itself (aggregating its own placement count
plus `CarManipulationController`'s reposition/scale/rotation counts via
`GestureCountsUpdatedEvent`) — this was deliberately kept as-is when
`CarManipulationController` was split out, since every *other* telemetry send
in the project follows the same inline pattern: `ArViewportController` sends
its own `VehicleInteraction` telemetry, `EstimatorCardController` sends its
own `AffordabilitySession` telemetry from `OnDisable`, `GameManager` sends
`CustomerSession`. Pulling only `ArSession`'s send into its own class would
make that one type inconsistent with the other three, not more consistent.
The real fix, when it's worth doing, is a broader pass across all four at
once — either a shared telemetry-sending pattern or dedicated sender classes
for each — not a one-off extraction on just this one.

## Recurring gotcha: inline UXML styles silently override USS class rules

This has caused real, hard-to-spot bugs multiple times in this project. Inline
`style="..."` attributes on a UXML element always beat a USS class rule of the same
specificity, even a rule added later. Before changing a USS class to fix a visual
issue, **check the UXML for an inline style on that same element** — if the element
already has an inline value for the property being changed, editing only the USS
does nothing.

## Recurring gotcha: script GUIDs

Never delete-and-recreate a `.cs` file to make a large change — deleting changes the
file's GUID, and any Inspector-serialized reference to that script (component on a
GameObject, prefab) breaks ("Missing Script"). Edit files in place. Most controllers
in this project are added via `AddComponent<T>()` in code rather than serialized in
the Inspector, so this specifically matters for anything that *is* Inspector-wired
(e.g. `ParentPageController`'s `VisualTreeAsset` fields).

## Tigris storage (3D models)

**The app holds no storage credentials.** To show a car, `SelectedCarModelLoader` calls
`GameManager.DownloadModel(vehicleId, modelKey)` -> `ModelDownloader`: (1) `GET api/customer/vehicles/{id}/model-url` (customer token)
returns a short-lived pre-signed link for exactly that vehicle's file, (2) a plain `UnityWebRequest` fetches the file from
the link. The API signs with the environment's read-only Tigris key, which lives in the API host's settings
(`Storage__*`), never in this repo or the build. Which bucket is used follows from which API the app talks to
(`CurrentEnvironment`), so there is no storage config in the app at all. Full convention, key handling and the
backend endpoint: `NAS_Backend/docs/STORAGE.md`. For **3D AR assets only**, not 2D catalog thumbnails.

**Models are cached on the device** (`ModelDiskCache` in `persistentDataPath/models`, 500 MB, least recently used out first; rules in
`ModelCacheRules`). A cached car is still asked for a link, then fetched with `If-None-Match`: Tigris answers 304 (use the file) or sends
the new one when the dealer replaced it. A download goes to `<name>.glb.part`; a dropped connection resumes with `Range` + `If-Match`
(412/416 restart from zero), 3 attempts. If the API or network is down but the car is cached, the cached copy is used.
`LoadingProgressEvent` drives the percentage on the loading overlay. Tigris' 206/304/412/416 behaviour was checked against the dev bucket.

Uploading is not done from the app or the Editor: use `NAS_Backend/tools/tigris-upload-model.sh`.
A model's key is `dealerships/{dealershipId}/vehicles/{vehicleId}/model.glb` (`vehicle_model.tigris_model_key`); the API
refuses any other key on a vehicle. A vehicle with no key, an inactive vehicle, or an API without storage settings
(`503 storage_not_configured`) makes the download fail, and the loader falls back to the placeholder car.

Deleted (11 Oct 2026): the AWS SDK plugin, `IStorageService`/`DevStorageService`/`DevStorageConfig`, `StoragePolicy` and the
per-environment storage config assets (they would have embedded a key in every build), the in-Editor uploader
(`TigrisStorageManager`), the AssetBundle experiment and the "asset manager" object in the AR scene. The `_useProduction` flag on
`GameManager` was removed (it was unused).

**Reaching Tigris from a terminal:** the buckets live in your Fly.io account. `fly auth login` (opens the browser), then
`fly storage list` / `fly storage dashboard <bucket>` to manage buckets and create access keys. For copying files use the
AWS CLI with a Tigris key: `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_ENDPOINT_URL_S3=https://fly.storage.tigris.dev`,
`AWS_REGION=auto`, then `aws s3 ls s3://nas-showroom-dev/`. Keys stay in your shell, never in git.

**Testing a download from an Editor script:** never block on a `Task` with `.GetAwaiter().GetResult()` (it deadlocked
Unity's main thread once); start it and check completion in a later call. `ModelDownloader` uses `UnityWebRequest`, so it
needs the main loop running (Play mode; `runInBackground` is on in this project).

## Design reference

`mobile-designs.tsx` (React/Tailwind) is the code-bundle export of the actual Figma
file ("NAS Showroom") and is the source of truth for exact copy, validation rules,
and layout intent where the Unity build is ambiguous or hasn't caught up yet. Notable
places the Unity build has intentionally diverged from it: car selection is a
swipe-carousel here vs. prev/next buttons there (deliberate choice, not a gap).

## Workflow rules

Every `.github/workflows/*.yml` starts with `# kind: ci | deploy | manual | scheduled` and follows `../NAS_Backend/docs/WORKFLOW_RULES.md`
(which triggers a kind may use, timeouts, concurrency, pinned actions, secrets only in deploys). `python3 .github/scripts/check_workflows.py`
checks it, and CI runs that check on every `.github/` change.
