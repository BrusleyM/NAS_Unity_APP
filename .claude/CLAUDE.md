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

**Dev / Staging / Production.** `AppEnvironment` has five values:
`Local`, `ApiDomain`, `ApiIp` (all "Dev": my machine, or my machine through
the nginx proxy) plus `Staging` and `Production` (hosted backends with real
certificates). Which one a build **actually uses** is
`EnvironmentPolicy.Effective(...)` (pure logic, EditMode-tested): Editor and
Development builds honour `GameManager._environment`; a **release build
ignores it** and is `Production` — or `Staging` when built with the scripting
define `NAS_STAGING_BUILD` — so a release can never ship pointing at
localhost. `GameManager.CurrentEnvironment` returns the effective value, and
`EnvironmentResolver` loads Staging/Production `ApiSettings` from
`Resources/Config/ApiSettings.<Env>.asset` (no Inspector wiring to forget);
a missing asset is a loud error, never a silent fall back to Local.
`TrustAnyCertificate` is true only for ApiDomain/ApiIp. Only Dev and Staging
really exist: Staging is the SmarterASP.NET site at `https://nas-api.neoxr.co.za`
(one site, one Postgres database on the trial plan). `Production` is defined but
not created, so its asset currently points at the same host; when production is
built, give it its own hostname and change that one asset. The full standard is
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
  `PointerUp` (25%-of-viewport-width threshold decides commit vs. snap back;
  either way, snaps `translate` back to `0` — no easing, this is intentional).
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
`carmodels/<key>.glb` (e.g. `carmodels/bmwm8demo.glb`), the DB's
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

**`AssetBundleTest.cs`** (same "asset manager" GameObject) is a manual dev/test
script demonstrating the (now-abandoned) AssetBundle download pattern — it's
currently disabled (`m_Enabled: 0` in `AR Scene.unity`) because its `Start()`
does a full upload/download/instantiate cycle every time the scene runs, which
raced `GameManager.Instance` initialization and threw on a cold Play-mode entry
straight into `AR Scene`. Left disabled; don't use it as a template for the
glTFast-runtime path above, its download mechanics (`AssetBundle.LoadFromMemory`)
don't apply there.

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
placeholder "Customize" bottom sheet (drag handle, header with close button, just
a "Coming soon" label — tapping the backdrop or the close button closes it), and
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

**Not yet built:** the camera-feed gradient scrim, and the real contents of the
"Customize" sheet (the Wheel/Paint/Trims/Dashboard 4-item grid, Paint's 5 color
swatches, the other three categories' text-choice rows) — the sheet currently
opens/closes but shows only placeholder text. Deliberately deferred — there's no
customer-facing data model yet for which options are actually configurable per car
(see the "Planned: API-driven vehicle catalog" section above; trim/color/interior/
wheel data was explicitly scoped out of the car-selection API work). Build this
grid once that data exists, following the
same pattern as the top bar (scene-placed controller in `AR Scene.unity`, not
`AddComponent`-dynamic — this scene only ever shows one screen, so it doesn't need
`ParentPageController`'s router pattern).

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

## Tigris storage

`GameManager` owns an `IStorageService` (`DevStorageService`) for uploading/
downloading 3D models by string key (`UploadModel`, `DownloadModel`, etc.) — this is
for **3D AR assets only**, not 2D catalog thumbnails. There's also a second,
separate manual-test path to the same bucket: `TigrisStorageManager` (its own S3
client, own `UploadFileAsync`/`DownloadObjectAsync`) on the "asset manager"
GameObject in `AR Scene.unity` — used directly (not through `IStorageService`) to
upload the demo cars' corrected `.glb` files. **If you drive `TigrisStorageManager` (or any
`Task`-returning upload/download call) from an Editor script via
`execute_code`-style reflection, never block on it with
`.GetAwaiter().GetResult()` in a loop** — this deadlocked Unity's main thread mid-
session after the first of 11 uploads (killed the Editor's responsiveness to
Stop/console entirely; only a lightweight ping-style check still answered). Fire
each call without awaiting it in the same script invocation, or drive it through a
coroutine, and verify completion out-of-band (e.g. list the bucket) instead of
blocking in-process.

`vehicle_model.tigris_model_key` (backend, nullable `varchar(200)`) is where each
row's Tigris object key lives — see "Vehicle catalog: real 3D models" above for the
current state of that join (uploaded and recorded in the DB, not yet consumed by
Unity for actual AR placement).

### Tigris MCP server (for AI-driven asset pipeline work, e.g. Blender passes)

`.mcp.json` at the repo root registers Tigris's official MCP server
(`@tigrisdata/tigris-mcp-server` via `npx`) for direct bucket access from an
agent — download/upload/list against `neo-ar-showroom` without going through
Unity at all, which matters when a Unity-driven task (Editor MCP tools) is
busy at the same time. The file is committed (references env var *names*
only, never actual secret values, matching this workspace's preference for
shareable/repo-based config over local-only setup — see
`feedback_shareable_tooling` in the AI memory system).

Each person running this needs three env vars set in their shell **before**
launching Claude Code (edited into the config mid-session doesn't take
effect until a full restart — Claude Code doesn't watch `.mcp.json` for
changes):
```
export AWS_ACCESS_KEY_ID="<Tigris access key from DevStorageConfig.asset>"
export AWS_SECRET_ACCESS_KEY="<Tigris secret key from DevStorageConfig.asset>"
export AWS_ENDPOINT_URL_S3="https://fly.storage.tigris.dev"
```
Same credentials `DevStorageService`/`TigrisStorageManager` already use
(`Assets/Resources/Config/DevStorageConfig.asset`) — this is a second way to
reach the same bucket, not a separate account. `${VAR}` interpolation in
`.mcp.json`'s `env` block is confirmed reliable on Claude Code **CLI**;
known unreliable on the Desktop app (upstream bug) — if driving this from
Desktop, set the values directly rather than relying on interpolation.

### TODO, not urgent — Tigris environment handling doesn't match Auth's pattern

`GameManager._useProduction` (the other field under its `[Header("Environment")]`,
alongside `_environment`/`AppEnvironment`) is currently **dead** — `InitializeStorage()`
always constructs `DevStorageService` regardless of its value; the bool only
feeds a `Debug.Log` line, so flipping it to `true` gets you a log claiming
"Using PRODUCTION storage" while Dev storage silently keeps running underneath.

**Deliberately not fixed now** — there's no plan for multiple Tigris storage
environments at this stage of the project. Revisit this when AR integration
starts (the AR placement step in the screen flow above, i.e. once vehicles
actually need real downloaded 3D models rather than just placeholder testing).
At that point, follow `AuthController`'s pattern: one enum on `GameManager`
as the single source of truth, not a second independent bool that can drift
(see the Auth section above for why that matters).

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
