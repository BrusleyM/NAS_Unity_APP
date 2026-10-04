# Engineering Report: NAS Showroom Unity AR Client (NEO AR Showroom)

**An event-driven UI Toolkit and AR Foundation client that selects a dealership, places a true-scale car, and reports what the customer did**

| | |
|---|---|
| **Document ID** | NAS-UN-ER-001 |
| **Version / date** | 1.0 / 5 October 2026 |
| **Project owner** | B. Masemola |
| **Prepared by** | Claude (Anthropic AI engineering assistant), working with the project owner |
| **Repository / branch** | `NEO AR Showroom` (Unity 6000.3.9f1), `vishnu/ml-telemetry-events` (local; not pushed) |
| **Related reports** | NAS_ML (NAS-ML-ER-001), NAS_Backend (NAS-BE-ER-001), NAS-Admin (NAS-AD-ER-001) |
| **Conventions** | IEEE numbering and citation style; tables captioned above, figures below; content structured to ECSA report expectations (problem definition, justified design, verification, ethics and impact, limitations) |

---

## Abstract

The Unity client is the customer-facing part of NAS Showroom: a customer signs in, chooses a dealership and a car, places a true-scale 3D model of it in their own space through AR, adjusts it, runs an affordability estimate, and optionally requests a test drive. The app is the source of every behavioural signal the lead classifier uses, so the quality of its telemetry bounds the quality of the whole system. This report covers five commits made between 27 August and 4 October 2026 that (i) stabilised AR placement, (ii) added a dealership-selection screen so a lead cannot start under the wrong dealership, (iii) added a test-drive request and session close-out, (iv) reported two new classifier features (`ar_load_failed`, `dealership_changed`), and (v) timed every screen and sent each visit to the backend. The code base is 100 C# files and 8 028 lines in 17 assemblies, communicating through 36 typed events on one bus. The first automated tests in the project, 14 EditMode tests for the screen-time logic, were verified by nine deliberate code mutations: six were caught at once and three survived, revealing real gaps (a boundary, a quit-while-backgrounded path, an empty screen name); three tests were added and all nine mutations are now caught. Four candidate demonstration car models were measured at 3.2 to 99.3 MB; the 99.3 MB model is a download-time risk that remains open. The report records what has not been tested on a device.

**Index Terms** — Unity, UI Toolkit, AR Foundation, event-driven architecture, telemetry, unit testing, mutation testing, glTF.

---

## 1. Introduction

### 1.1 Problem statement

A customer meets the system through this app, and the system's judgement of that customer comes from what the app reports. Five problems were identified:

1. **AR placement felt unreliable.** The placed car snapped when ARKit corrected tracking, the user could be placed inside a large vehicle, and a tap could land before tracking was stable.
2. **No dealership choice.** The app never said where the customer was buying, so the backend could only guess a lead's dealership from the vehicle.
3. **Two classifier signals were never reported:** whether the car model failed to load (a customer who gave up because nothing appeared looks very different from one who lost interest) and whether the customer changed dealership.
4. **No per-screen timing.** The session timer measured wall-clock time, including time with the app in the background.
5. **No automated tests.** The project had none, and the timing logic is exactly the kind that is easy to get subtly wrong.

### 1.2 Objectives

<p align="center"><b>TABLE I</b><br/><small>OBJECTIVES AND HOW EACH IS VERIFIED</small></p>

| ID | Objective | Verification |
|---|---|---|
| O1 | Placement is stable: eased anchor follow, guided tracking start, footprint-aware clearance | Code review against the commit's list; device test by the project owner (not yet done) |
| O2 | The customer chooses a dealership; the car list, session and requests all carry it | Backend 409 test (NAS-BE-ER-001); app flow compiled, device test pending |
| O3 | `ar_load_failed` and `dealership_changed` events reach the backend | Compiled; backend accepts them (NAS-BE-ER-001) |
| O4 | Time on each screen is recorded without counting background time, and survives a force-quit up to the last screen change | 14 EditMode tests on the pure tracker; 9 mutations |
| O5 | The session timer stays unchanged beside the screen timer | Design (separate classes); see Fig. 4 |

---

## 2. Architecture and design

### 2.1 Event-driven structure

Controllers never hold references to each other; they communicate through a static, type-safe publish/subscribe bus (`EventBus`) carrying 36 immutable event structs (Fig. 1). A controller subscribes in `OnEnable` and unsubscribes in `OnDisable`, so a destroyed object stops receiving events. Table II gives the size of the code base by area.

<p align="center"><img src="figures/fig01_event_architecture.png" width="760" alt="Fig. 1"/></p>
<p align="center"><small>Fig. 1. Event-driven structure: controllers publish and subscribe on one bus; GameManager is the only subscriber to the raw domain events and republishes Session events after updating its state.</small></p>



<p align="center"><b>TABLE II</b><br/><small>SIZE OF THE CODE BASE (C# UNDER `ASSETS/SCRIPTS`, MEASURED 5 OCTOBER 2026)</small></p>

| Area | Contents | Files | Lines |
|:---|:---|---:|---:|
| `Core` | Auth, networking, events, models, services, AR controllers, `GameManager` | 80 | 4,756 |
| `UI Docs` | Screen controllers and carousel components | 10 | 2,622 |
| `base scripts` | Base classes and utilities | 3 | 350 |
| `Storage` | Object-storage client | 3 | 162 |
| `AR Scripts` | AR helper scripts | 2 | 73 |
| `Configurations` | Settings assets code | 2 | 65 |
| **Production total** |  | **100** | **8,028** |
| `Assets/Tests/EditMode` | EditMode tests (NUnit) | 1 | 211 |

**Decision D1 — a deliberate hybrid for session state.** `GameManager` (a `DontDestroyOnLoad` singleton) is the only subscriber to the raw domain events `AuthSucceededEvent`, `CarSelectedEvent` and `DealershipSelectedEvent`. After it has updated its own state it republishes `SessionAuthenticatedEvent`, `SessionCarSelectedEvent` and `SessionDealershipSelectedEvent`, and every other script subscribes to those. The reason is a real defect: a screen controller read the access token from inside the authentication event's own handler chain, before `GameManager` had stored it, and the vehicle-catalog request went out with a null token and was rightly rejected. Moving `GameManager`'s subscriptions from `OnEnable` to `Awake` fixed the symptom by execution-order luck and is kept as defence in depth; the republished `Session*Event` is the structural fix, because a subscriber can only observe state after it is set, whatever the subscription order. The alternative, every consumer subscribing to the raw event, was rejected as ongoing overhead with no extra safety. A known deviation remains: several scripts still read `GameManager.Instance` directly, recorded as a dedicated future pass rather than piecemeal change.

**Decision D2 — `AddComponent` then `Initialize`.** `AddComponent<T>()` runs `OnEnable` synchronously, before the creator can hand anything over, so controllers that need data (for example a `VisualTreeAsset`) get it through an explicit `Initialize(...)` and keep `OnEnable` to wiring that needs no such data.

**Decision D3 — one assembly per `Core` folder.** The code is split into 17 assemblies so a dependency must be declared. The trap is a transient "no errors" result from Unity's incremental compiler after adding a cross-folder reference, which later surfaces as `CS0103`; the project convention is to confirm in Play mode, which refuses to start with a real compile error.

### 2.2 Screen flow

Fig. 2 shows the flow and Table III lists the seven named screens. Login, registration, dealership selection, car selection and the estimator are UI Toolkit cards that the router (`ParentPageController`) swaps inside one scene; the AR viewport is a separate Unity scene. The router publishes `ScreenShownEvent` whenever a different screen becomes visible.

<p align="center"><img src="figures/fig02_screen_flow.png" width="760" alt="Fig. 2"/></p>
<p align="center"><small>Fig. 2. Screen flow: cards inside the Main App scene, then the separate AR scene, then back.</small></p>



<p align="center"><b>TABLE III</b><br/><small>NAMED SCREENS (`SCREENNAMES`) AND WHERE THEY LIVE</small></p>

| Screen id | Where | Notes |
|---|---|---|
| `splash` | Main App, card | Before sign-in |
| `login` | Main App, card | Client-side validation, then API |
| `register` | Main App, card | Per-field errors from client-side validation |
| `dealership_selection` | Main App, card | New; lists dealerships with active vehicles; last choice highlighted, never auto-selected |
| `car_selection` | Main App, card | Swipe carousel with a 3-slot pool; shows the dealership with a Change control |
| `ar_viewport` | AR Scene | Whole scene counts as one screen |
| `estimator` | Main App, card | Affordability calculator and estimate submission |

### 2.3 Dealership selection

After login the customer picks a dealership (`DealershipSelectionController`, UXML loaded from `Resources` so no scene edit was needed). The choice publishes `DealershipSelectedEvent`; `GameManager` sets `SelectedDealership`, clears the selected car if the dealership changed, republishes `SessionDealershipSelectedEvent`, and tells the backend, deferring the call until the telemetry session exists. The car list is requested for that dealership; the estimator and test-drive requests send the session id so the backend can answer 409 for a mismatched vehicle (NAS-BE-ER-001). QR-code scanning is a possible later version; the selection screen is the interim design. Choosing a different dealership after an earlier choice is logged as `dealership_changed`; the first choice is not a change.

### 2.4 AR placement pass

Table IV lists what the placement commit (27 August 2026) added. Each item answers a specific observed problem.

<p align="center"><b>TABLE IV</b><br/><small>AR PLACEMENT STABILITY AND USABILITY CHANGES</small></p>

| Component | Change | Problem addressed |
|---|---|---|
| `AnchorFollowSmoother` | The car eases toward its anchor's corrected pose | ARKit tracking corrections made the car visibly pop |
| `ArCoachingOverlayController` | Drives ARKit's native coaching UI | No guidance while tracking was initialising |
| `ContactShadowFactory` | Procedural soft shadow sized from the real footprint | Residual float under the car was obvious |
| `ArLightEstimationController` | ARKit light estimate drives the directional light | The car did not match room lighting |
| `ObjectPlacerController` | Clearance from the camera computed from the rotated footprint; placement gated on a minimum plane-tracking time | A flat 1 m clearance left the user inside large vehicles; taps could land too early |
| `CarManipulationController` | Drag scales with zoom, ignores touches that began on UI, guards a raycast behind the camera; vertical-offset slider and reset button | Controls fought the UI and misbehaved at high zoom |
| `ArViewportController` | Sliders and Customize stay hidden until a car is placed | Controls with nothing to act on |
| Always Included Shaders | Contact-shadow shader registered | `Shader.Find` returned null on device |

### 2.5 Real 3D models at runtime

`SelectedCarModelLoader` downloads the selected vehicle's `.glb` from object storage at the start of the AR scene and builds it with glTFast's runtime API, then hands it to the placement service. glTF is a portable format that glTFast parses identically on every platform [1], which avoids the per-platform build and upload duplication that AssetBundles need. Every failure point (no model key, download, parse, instantiate) falls back to the placeholder prefab so placement is never blocked, and each publishes `ArModelLoadFailedEvent` with a reason code so the failure becomes a classifier feature.

The source models needed correction before they were usable: several imported at the wrong scale (about 100 times too small to 5–10 times too large), and all originals stood on their nose with an off-centre pivot. Both were fixed at the source in Blender rather than patched in Unity: parts joined, the full evaluated world matrix applied, a uniform scale computed against the vehicle's real length, the pivot moved to the ground-touching centroid, and re-exported. Table V and Fig. 3 give the size of four processed candidates for the demonstration catalogue (a plan that is paused until the AR placement pass has been tested), measured on 5 October 2026.

<p align="center"><img src="figures/fig04_model_sizes.png" width="420" alt="Fig. 3"/></p>
<p align="center"><small>Fig. 3. Size of the four processed candidate models; the dark bar exceeds 50 MB.</small></p>



<p align="center"><b>TABLE V</b><br/><small>PROCESSED CANDIDATE DEMONSTRATION MODELS</small></p>

| Model (processed .glb) | Size (MB) | Over 50 MB |
|:---|---:|:---:|
| `ds-etense` | 3.20 | No |
| `conceptcar` | 3.41 | No |
| `ray` | 4.95 | No |
| `sedan` | 99.33 | Yes |
| **Median** | **4.18** |  |

### 2.6 Telemetry the app sends

<p align="center"><b>TABLE VI</b><br/><small>TELEMETRY SENT BY THE APP</small></p>

| Signal | Endpoint (backend) | Feeds |
|---|---|---|
| Session start and end | `/api/telemetry/session`, `/session/end` | Duration, session count |
| Dealership chosen | `/api/telemetry/session/dealership` | Scoping, `dealership_changes` |
| Events (`vehicle_viewed`, `ar_load_failed`, `dealership_changed`) | `/api/telemetry/events` | Classifier features `ar_load_failures`, `dealership_changes` |
| AR session | `/api/telemetry/ar-sessions` | Placements, repositions, scales, rotations |
| Calculator session | `/api/telemetry/affordability-sessions` | Calculator depth, final values |
| Vehicle interaction | `/api/telemetry/vehicle-interactions` | Colour changes, time per car |
| Screen visit | `/api/telemetry/screen-visits` | Time per screen (not yet a model feature) |
| Estimate; test-drive request | `/api/estimator/submissions`, `/test-drive-requests` | Lead creation; the strongest intent signal |

Events with no vehicle send `vehicleModelId` 0, because `JsonUtility` cannot serialise a null integer; the backend stores a value of 0 or below as null.

### 2.7 Two counters: session time and screen time

The session timer is unchanged. Alongside it, `ScreenVisitTracker` (pure C#, no Unity dependency) turns `ScreenShownEvent`s into visits (Fig. 4). The rules are small and each has a test: a visit is sent when the customer leaves the screen; showing the same screen again does not start a new visit (the router re-renders cards); a visit shorter than 0.5 s is not a visit; pausing the app closes and sends the visit, remembers the screen and resumes it with a fresh clock, so background time is not screen time and a force-quit loses nothing before the last screen change; quitting closes for good. Each visit has its own id so the backend can discard a retry. Visits made before sign-in are held (capped at 50) and sent once the session has an id.

<p align="center"><img src="figures/fig03_two_counters.png" width="760" alt="Fig. 4"/></p>
<p align="center"><small>Fig. 4. The two counters on the unit-test scenario: a 20 s estimator visit, ten minutes in the background, then 15 s more. The session timer reads 635 s; screen time is 20 s + 15 s = 35 s.</small></p>



**Decision D4 — hide an empty carousel slot with `visibility`, not `display`.** `display: none` removes the element from flex layout, which breaks the 3-slot symmetry that keeps the current card centred.

**Decision D5 — validate before the network.** Authentication checks every field client-side, matching the design reference, and publishes per-field errors, so most mistakes never cost a request.

---

## 3. Implementation record

<p align="center"><b>TABLE VII</b><br/><small>COMMITS OF THIS CAMPAIGN IN THE UNITY PROJECT (OLDEST FIRST)</small></p>

| Commit | Date | Change |
|:---|:---|:---|
| 4b49c6d | 2026-08-27 | Add AR placement stability/UX pass: anchor smoothing, coaching overlay, contact shadow, light estimation, footprint-aware clearance |
| 5346139 | 2026-10-04 | Add AR "Request test drive" button, close out customer sessions, support several doors per car |
| 8013b6f | 2026-10-04 | Add dealership selection screen so leads start under the dealership the customer chose |
| 8e7f94b | 2026-10-04 | Report ar_load_failed and dealership_changed telemetry events for the buyer classifier |
| efe71ad | 2026-10-04 | Time each app screen and send the visits as telemetry; add the first EditMode tests |


---

## 4. Verification and validation

### 4.1 EditMode tests

The first Unity tests in the project test `ScreenVisitTracker`. They run in the Test Runner's EditMode tab; the logic has no engine dependency, so for this report the same file and the tracker source were also compiled against NUnit 3 in a stand-alone project and run there (14 passed). Table VIII lists them.

<p align="center"><b>TABLE VIII</b><br/><small>`SCREENVISITTRACKERTESTS`</small></p>

| # | Test | Behaviour asserted (from the test name) |
|---:|:---|:---|
| 1 | `MovingToAnotherScreenRecordsTimeOnTheOneLeft` | Moving to another screen records time on the one left. |
| 2 | `NothingIsSentUntilTheCustomerLeavesTheScreen` | Nothing is sent until the customer leaves the screen. |
| 3 | `ShowingTheSameScreenAgainDoesNotStartANewVisit` | Showing the same screen again does not start a new visit. |
| 4 | `AScreenThatFlashesByIsNotAVisit` | A screen that flashes by is not a visit. |
| 5 | `BackgroundedTimeIsNotCountedAsTimeOnScreen` | Backgrounded time is not counted as time on screen. |
| 6 | `AVisitIsSentAtPauseSoAForceQuitLosesNothingBeforeIt` | A visit is sent at pause so a force quit loses nothing before it. |
| 7 | `IfTheScreenChangesWhileBackgroundedTheNewOneIsReopenedOnResume` | If the screen changes while backgrounded the new one is reopened on resume. |
| 8 | `QuittingClosesTheCurrentScreenAndDoesNotReopenIt` | Quitting closes the current screen and does not reopen it. |
| 9 | `PausingWithNothingShownDoesNothing` | Pausing with nothing shown does nothing. |
| 10 | `EveryVisitGetsItsOwnIdSoARetryCanBeRecognisedButTwoVisitsAreNot` | Every visit gets its own id so a retry can be recognised but two visits are not. |
| 11 | `AVisitOfExactlyTheMinimumLengthIsKept` | A visit of exactly the minimum length is kept. |
| 12 | `QuittingWhileBackgroundedDoesNotReopenTheScreenOnResume` | Quitting while backgrounded does not reopen the screen on resume. |
| 13 | `AnEmptyScreenNameIsIgnored` | An empty screen name is ignored. |
| 14 | `ScreenNamesAreSnakeCaseWhichIsWhatTheBackendAccepts` | Screen names are snake case which is what the backend accepts. |


### 4.2 Mutation checks

Each of nine rules in the tracker was broken in turn and the tests re-run. Six mutations failed a test straight away. Three survived, and each was a real gap rather than an equivalent mutant.

<p align="center"><b>TABLE IX</b><br/><small>MUTATION CHECKS ON `SCREENVISITTRACKER` (5 OCTOBER 2026)</small></p>

| # | Mutation | Tests failing on the 11-test suite | Outcome |
|:---|:---|:---|:---|
| U1 | Re-showing the same screen starts a new visit | 1 of 11 | Caught |
| U2 | No minimum-duration filter | 1 of 11 | Caught |
| U3 | Boundary: `< 0.5 s` becomes `<= 0.5 s` | none | **Survived** → `AVisitOfExactlyTheMinimumLengthIsKept`; now fails 1 of 14 |
| U4 | Pause does not close the visit | 3 of 11 | Caught |
| U5 | Resume does not restart the clock | 2 of 11 | Caught |
| U6 | Quit leaves the resume screen set | none | **Survived** → `QuittingWhileBackgroundedDoesNotReopen…`; now fails 1 of 14 |
| U7 | Every visit gets the same id | 1 of 11 | Caught |
| U8 | Screen change while backgrounded is ignored | 1 of 11 | Caught |
| U9 | Empty screen name is accepted | none | **Survived** → `AnEmptyScreenNameIsIgnored` (rewritten once); now fails 1 of 14 |


Three tests were added (14 in total, from 11). Two killed their mutation at once; the empty-name test survived once more in its first form, because it called the methods in an order that hid the mutation, and was rewritten until it failed against it. All nine mutations are now caught.

### 4.3 What has not been verified

The commits were made on separate branches because the project owner had not yet tested them. No claim in this report about placement quality, the dealership screen or the screen-visit calls on a physical device is a measurement. In particular the test-drive button needs a placed car and was verified only to compile and lay out.

---

## 5. Limitations and risks

1. **Test coverage is narrow.** 14 tests cover one 119-line class; the remaining roughly 7 900 lines have none. The architecture (events, pure logic) makes more testable code possible, but only this part exists.
2. **No retry after a failed model load.** After any failure the placeholder is used for the rest of the AR session; recovery means leaving and re-entering the scene. The right behaviour (button, automatic retry, number of attempts) is a product decision, not yet taken.
3. **Download size.** One candidate model is 99.3 MB. On a mobile connection that is a long wait before the car appears, and a failed or abandoned wait is exactly what `ar_load_failed` records. Reducing its textures or geometry should precede shipping it.
4. **Direct `GameManager.Instance` reads** remain in several scripts (coupling and testability, not a known defect).
5. **Storage environment flag.** `_useProduction` on `GameManager` currently only changes a log line; storage always uses the development configuration. It should follow the environment enum used for the API address once a second storage environment exists.
6. **Unity tooling churn.** `Packages/packages-lock.json` changes whenever the editor opens the project and was deliberately not committed.

## 6. Ethics, privacy and impact

- **Behavioural tracking.** The app records which screens a customer visits and for how long, in addition to AR and calculator behaviour. Customers should be told what is recorded and why before real use, and the retention question raised in NAS-BE-ER-001 applies here too.
- **Background time.** Pausing the screen timer when the app is backgrounded avoids recording time the customer was not using it.
- **Branding.** The demonstration catalogue is deliberately named "DEMO" so nothing in the shipped app implies a partnership with a manufacturer.
- **Accessibility.** Placement relies on pinch, drag and slider gestures; users who cannot perform them have no alternative in this version.

## 7. Recommendations

1. Test the five commits on a device and record placement stability, model load time and the dealership flow.
2. Decide the failed-model retry behaviour and implement it with the cleanup the loader already does at its start.
3. Reduce the 99.3 MB model and set a size budget for catalogue models.
4. Move `GameManager.Instance` reads to events in one dedicated pass.
5. Extend EditMode tests to the other pure logic (pager boundaries, validation rules).

---

## References

[1] Khronos Group, "glTF 2.0 Specification," 2021.
[2] Unity Technologies, "UI Toolkit," Unity Manual (online documentation).
[3] Unity Technologies, "AR Foundation," Unity Manual (online documentation).
[4] NAS_Backend, "Engineering Report: Dealership-Scoped Showroom API," NAS-BE-ER-001, Oct. 2026.
[5] NAS_ML, "Engineering Report: Buyer-Intent Classification Service," NAS-ML-ER-001, Oct. 2026.
[6] Y. Jia and M. Harman, "An analysis and survey of the development of mutation testing," *IEEE Trans. Softw. Eng.*, vol. 37, no. 5, pp. 649–678, 2011.

## Appendix A — Reproducing the numbers

```bash
python3 docs/figures/make_figures.py        # regenerates fig01–fig04 (model sizes read from docs/data/model_sizes.json)
# Run the 14 tests in Unity: Window > General > Test Runner > EditMode > Run All
```
