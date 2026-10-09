using NAS.Core.Interfaces;
using NAS.Core.Events;
using NAS.Core.Networking;
using NAS.Storage;
using NAS.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NAS.Core.Models;
using UnityEngine;

namespace NAS.Core
{
    /// <summary>
    /// Holds session state and owns the storage service. It no longer has its
    /// CurrentUser/SelectedCar properties set directly by whichever controller
    /// happens to be handling a click — it derives them by listening to the
    /// same domain events everything else reacts to.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Environment")]
        [Tooltip("If true, uses production Cognito settings; otherwise uses development basic credentials.")]
        [SerializeField] private bool _useProduction = false;

        [Tooltip("Project-wide switch for which backend API endpoint to use. Local = http://localhost:5080 (safe default, always works). ApiDomain = https://api.nas.test:8443 via the optional local nginx proxy (NAS_Backend/nginx/README.md) - only works on a device that resolves api.nas.test (this Mac via /etc/hosts, or another device via dnsmasq's device-DNS override). ApiIp = same nginx proxy, addressed by raw LAN IP instead of the hostname - for a device on a network where nothing resolves api.nas.test (e.g. a phone hotspot without the dnsmasq override set up); update the ApiIp ApiSettings asset's URL when the LAN IP changes. Keep this at Local in anything committed/pushed, or teammates without that setup will have auth silently fail. AuthController reads this in Start() rather than owning its own toggle.")]
        [SerializeField] private AppEnvironment _environment = AppEnvironment.Local;
        // What a build really uses, which is not always what is selected above: a release build
        // ignores the dropdown (Production, or Staging with the NAS_STAGING_BUILD scripting
        // define) so it cannot ship pointing at a developer machine. See EnvironmentPolicy.
#if NAS_STAGING_BUILD
        private const bool StagingBuild = true;
#else
        private const bool StagingBuild = false;
#endif
        public AppEnvironment CurrentEnvironment => EnvironmentPolicy.Effective(_environment, Debug.isDebugBuild, StagingBuild);

        [Tooltip("Used when CurrentEnvironment is Local. Single project-wide reference - every feature that talks to the API resolves its ApiSettings through EnvironmentResolver.Resolve(), which reads these three fields, instead of each controller wiring its own copies.")]
        [SerializeField] private ApiSettings _apiSettings;
        public ApiSettings ApiSettings => _apiSettings;

        [Tooltip("Used when CurrentEnvironment is ApiDomain.")]
        [SerializeField] private ApiSettings _apiDomainSettings;
        public ApiSettings ApiDomainSettings => _apiDomainSettings;

        [Tooltip("Used when CurrentEnvironment is ApiIp.")]
        [SerializeField] private ApiSettings _apiIpSettings;
        public ApiSettings ApiIpSettings => _apiIpSettings;

        [Header("Session")]
        public User CurrentUser { get; private set; }
        public string AccessToken { get; private set; }
        public VehicleInfo SelectedCar { get; private set; }
        // The dealership the customer chose this login. Null until they pick
        // one (reset on every login - they might be somewhere else today).
        public DealershipInfo SelectedDealership { get; private set; }
        // Last dealership picked on this device, remembered across launches
        // only so the list can highlight it - never auto-selected.
        private const string LastDealershipIdPrefKey = "NAS.LastDealershipId";
        public int LastDealershipId => PlayerPrefs.GetInt(LastDealershipIdPrefKey, 0);
        public bool ReturnToEstimator { get; set; } = false;
        // Set by ParentPageController once the splash card has been shown/dismissed
        // for this app session, so returning to "Main App" from the AR scene (Back/
        // Confirm both reload this scene) doesn't show the splash again every time -
        // only once, on cold start. Lives here rather than as a scene-local bool on
        // ParentPageController because that controller (and its GameObject) gets
        // torn down and recreated on every "Main App" scene load; GameManager is the
        // DontDestroyOnLoad singleton that actually survives across it.
        public bool HasShownSplash { get; set; } = false;
        // Set once AR Scene has been additively loaded for the first time this
        // app session. AR Scene is deliberately never unloaded/reloaded after
        // that - re-entering AR toggles visibility/tracking instead of a full
        // scene reload, which is what was causing a black camera feed on the
        // second+ entry (destroying and recreating ARSession/XROrigin is not
        // the AR Foundation-documented pattern; disabling/re-enabling is).
        public bool IsArSceneLoaded { get; set; } = false;
        // Set by ArViewportController after the Confirm button's best-effort
        // call to POST /api/customer/configurations succeeds (0 if that call
        // hasn't happened yet or failed - matches the same "0 means unset"
        // convention the backend already expects, since Unity's JsonUtility
        // can't send a real null for an unset int). EstimatorCardController
        // reads this directly (a plain field, no Session*Event) rather than
        // reacting to a car-selection-style event, since nothing needs it
        // synchronously as part of an event chain - it's just read later,
        // once the Estimator screen is shown.
        public int SelectedConfigurationId { get; set; } = 0;
        // Server-assigned id from POST /api/telemetry/session, captured once
        // StartTelemetrySession()'s best-effort call succeeds (0 = not
        // started yet, or the call failed) - same "0 means unset" convention
        // as SelectedConfigurationId above. Every other telemetry call
        // (vehicle interactions, AR sessions, affordability sessions) needs
        // this int, not a client-generated string id, to satisfy
        // TelemetryService.ValidateCustomerSessionAsync on the backend.
        public int TelemetrySessionId { get; set; } = 0;
        // Guards EndTelemetrySession() against firing more than once - the
        // app can background/foreground (OnApplicationPause toggling false
        // then true) many times in one real usage session, and quit can
        // follow a pause that already sent the end signal. Only the first
        // call should count as "the session ended" (see EndTelemetrySession's
        // own comment for why re-opening a session on resume isn't handled).
        private bool _telemetrySessionEnded = false;
        // Set by SelectedCarModelLoader after each load attempt - true only
        // if the customer's actual selected car model loaded (not a
        // placeholder-prefab fallback from a download/parse/instantiate
        // failure). Reset to false at the start of every attempt, before the
        // async work begins. Read by ObjectPlacerController before sending
        // AR-session telemetry on AR exit: an AR visit spent looking at (or
        // immediately backing out of because of) a technical failure isn't
        // genuine placement behaviour and shouldn't be recorded as if it
        // were "customer looked and lost interest". Not event-chain-critical
        // (nothing reacts to it synchronously) - just read later, same
        // pattern as SelectedConfigurationId above.
        public bool CurrentArModelLoadSucceeded { get; set; } = false;

        private IStorageService _storage;

        // Times each screen the customer spends time on (see ScreenVisitTracker).
        // Visits can finish before there is anywhere to send them - splash, login
        // and register all come before the telemetry session exists - so they wait
        // here and are sent as soon as it does.
        private ScreenVisitTracker _screens;
        private readonly System.Collections.Generic.List<ScreenVisitTracker.Visit> _pendingVisits =
            new System.Collections.Generic.List<ScreenVisitTracker.Visit>();
        private const int MaxPendingVisits = 50;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeStorage();
            _screens = new ScreenVisitTracker(OnScreenVisitFinished);

            // Subscribing here rather than in OnEnable() is deliberate: Unity
            // guarantees every object's Awake() finishes before any object's
            // OnEnable() runs, within the same load - but does NOT guarantee
            // Awake()/OnEnable() ordering ACROSS different GameObjects
            // otherwise. This is what let a real bug happen: a UI controller
            // read GameManager.AccessToken before GameManager's own handler
            // for that login had run yet, sending an unauthenticated request
            // ("Please sign in to continue" on device). The actual structural
            // fix for THAT class of bug is the Session*Event pattern below
            // (see AuthSucceededEvent/SessionAuthenticatedEvent's doc comments
            // in GameEvents.cs) - subscribing here in Awake() is kept anyway
            // as defense in depth, so GameManager is guaranteed ready before
            // anything else in the scene, for any event it's ever given.
            EventBus.Subscribe<AuthSucceededEvent>(OnAuthSucceeded);
            EventBus.Subscribe<ScreenShownEvent>(OnScreenShown);
            EventBus.Subscribe<DealershipSelectedEvent>(OnDealershipSelected);
            EventBus.Subscribe<ArModelLoadFailedEvent>(OnArModelLoadFailed);
            EventBus.Subscribe<CarSelectedEvent>(OnCarSelected);
            EventBus.Subscribe<ReturnToEstimatorRequestedEvent>(OnReturnToEstimatorRequested);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<AuthSucceededEvent>(OnAuthSucceeded);
            EventBus.Unsubscribe<ScreenShownEvent>(OnScreenShown);
            EventBus.Unsubscribe<DealershipSelectedEvent>(OnDealershipSelected);
            EventBus.Unsubscribe<ArModelLoadFailedEvent>(OnArModelLoadFailed);
            EventBus.Unsubscribe<CarSelectedEvent>(OnCarSelected);
            EventBus.Unsubscribe<ReturnToEstimatorRequestedEvent>(OnReturnToEstimatorRequested);
        }

        // OnApplicationPause(true) is the reliable "session ended" signal on
        // mobile - most real sessions end with the user backgrounding the
        // app (home button/app switcher), not force-quitting it, and a
        // backgrounded app is NOT reliably given a chance to run further
        // code once it's actually quit. OnApplicationQuit is kept too, as a
        // fallback for platforms/cases where pause never fires before quit
        // (e.g. stopping Play mode in the Editor) - _telemetrySessionEnded
        // stops both from double-sending if pause already fired.
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                // Close the current screen first, so its visit is sent before the session end.
                _screens?.Pause();
                EndTelemetrySession();
            }
            else
            {
                _screens?.Resume();
            }
        }

        private void OnApplicationQuit()
        {
            _screens?.Stop();
            EndTelemetrySession();
        }

        // Publishing the Session*Event AFTER the field assignment (not
        // before) is the actual guarantee here - it's what makes it
        // structurally impossible for a subscriber to observe this event
        // before the state it describes is set, regardless of subscription
        // order. Don't reorder these.
        private void OnAuthSucceeded(AuthSucceededEvent evt)
        {
            CurrentUser = evt.User;
            AccessToken = evt.AccessToken;
            SelectedDealership = null; // chosen fresh every login
            EventBus.Publish(new SessionAuthenticatedEvent(CurrentUser, AccessToken));
            StartTelemetrySession();
        }

        private void OnScreenShown(ScreenShownEvent evt) => _screens?.Show(evt.ScreenName);

        // Best-effort like all telemetry. Sent now if the session exists, otherwise
        // held (up to a cap) and sent when StartTelemetrySession gets its id.
        private void OnScreenVisitFinished(ScreenVisitTracker.Visit visit)
        {
            if (TelemetrySessionId > 0 && !string.IsNullOrEmpty(AccessToken))
            {
                SendScreenVisit(visit);
                return;
            }
            if (_pendingVisits.Count < MaxPendingVisits)
                _pendingVisits.Add(visit);
        }

        private void FlushPendingScreenVisits()
        {
            if (TelemetrySessionId <= 0 || string.IsNullOrEmpty(AccessToken)) return;
            foreach (var visit in _pendingVisits)
                SendScreenVisit(visit);
            _pendingVisits.Clear();
        }

        private void SendScreenVisit(ScreenVisitTracker.Visit visit)
        {
            var resolved = EnvironmentResolver.Resolve("[NAS Telemetry]");
            if (resolved.Settings == null) return;

            var telemetryApi = new TelemetryApi(this, resolved.Settings, resolved.TrustAnyCertificate);
            var request = new ScreenVisitTelemetryRequest
            {
                customerSessionId = TelemetrySessionId,
                clientScreenVisitId = visit.ClientId,
                screenName = visit.Screen,
                startedAt = visit.StartedAt.ToString("o"),
                endedAt = visit.EndedAt.ToString("o")
            };
            telemetryApi.LogScreenVisit(request, AccessToken, result =>
            {
                if (!result.Success)
                    Debug.LogWarning($"[NAS Telemetry] screen visit '{visit.Screen}' failed: {result.Error.Detail}");
            });
        }

        private void OnDealershipSelected(DealershipSelectedEvent evt)
        {
            var dealership = evt.Dealership;
            if (dealership == null || dealership.id <= 0) return;

            // A car (and any saved configuration) from another dealership's
            // catalog must not carry over.
            var changed = SelectedDealership != null && SelectedDealership.id != dealership.id;
            if (SelectedDealership == null || changed)
            {
                SelectedCar = null;
                SelectedConfigurationId = 0;
            }
            SelectedDealership = dealership;
            PlayerPrefs.SetInt(LastDealershipIdPrefKey, dealership.id);
            EventBus.Publish(new SessionDealershipSelectedEvent(dealership));
            ApplyDealershipToTelemetrySession();
            // Switching after already choosing is a signal in itself (picked the
            // wrong place, or shopping around); the first choice is not.
            if (changed)
                LogActivityEvent("dealership_changed", 0);
        }

        // The selected car's model could not be loaded - the customer is looking
        // at a placeholder (or nothing), which explains an abandoned AR visit.
        private void OnArModelLoadFailed(ArModelLoadFailedEvent evt) =>
            LogActivityEvent("ar_load_failed", SelectedCar != null ? SelectedCar.id : 0);

        // Tells the backend which dealership this session is for, so the
        // lead/activity it produces is shown to that dealership's staff.
        // Safe to call again (idempotent server-side). If the telemetry
        // session hasn't started yet (id still 0), StartTelemetrySession's
        // callback calls this once it has.
        private void ApplyDealershipToTelemetrySession()
        {
            if (SelectedDealership == null || TelemetrySessionId <= 0 || string.IsNullOrEmpty(AccessToken)) return;

            var resolved = EnvironmentResolver.Resolve("[NAS Telemetry]");
            if (resolved.Settings == null) return;

            var telemetryApi = new TelemetryApi(this, resolved.Settings, resolved.TrustAnyCertificate);
            var request = new SetSessionDealershipRequest
            {
                customerSessionId = TelemetrySessionId,
                dealershipId = SelectedDealership.id
            };
            telemetryApi.SetSessionDealership(request, AccessToken, result =>
            {
                if (!result.Success)
                    Debug.LogWarning($"[NAS Telemetry] Failed to set session dealership: {result.Error.Detail}");
            });
        }

        private void OnCarSelected(CarSelectedEvent evt)
        {
            SelectedCar = evt.Vehicle;
            SelectedConfigurationId = 0; // belonged to whatever car was previously selected
            EventBus.Publish(new SessionCarSelectedEvent(SelectedCar));
            LogVehicleViewedEvent(SelectedCar);
        }
        private void OnReturnToEstimatorRequested(ReturnToEstimatorRequestedEvent evt) => ReturnToEstimator = true;

        // Best-effort, fire-and-forget - a failed/slow telemetry call must
        // never block or delay login. TelemetrySessionId simply stays 0,
        // which every other telemetry send treats as "session not ready,
        // skip this call" rather than retrying or queuing.
        private void StartTelemetrySession()
        {
            if (string.IsNullOrEmpty(AccessToken)) return;

            var resolved = EnvironmentResolver.Resolve("[NAS Telemetry]");
            if (resolved.Settings == null) return;

            var telemetryApi = new TelemetryApi(this, resolved.Settings, resolved.TrustAnyCertificate);
            var request = new CustomerSessionTelemetryRequest
            {
                clientSessionId = Guid.NewGuid().ToString(),
                startedAt = DateTime.UtcNow.ToString("o"),
                appVersion = Application.version,
                platform = Application.platform.ToString(),
                deviceType = SystemInfo.deviceModel
            };
            telemetryApi.StartSession(request, AccessToken, result =>
            {
                if (result.Success)
                {
                    TelemetrySessionId = result.Value.id;
                    // The customer may have picked a dealership before this call returned.
                    ApplyDealershipToTelemetrySession();
                    // Screens they already left (splash, login...) were waiting for this id.
                    FlushPendingScreenVisits();
                }
                else
                    Debug.LogWarning($"[NAS Telemetry] Failed to start session: {result.Error.Detail}");
            });
        }

        // Closes out the session server-side so CustomerSession.EndedAt (and
        // its computed DurationSeconds, which buyer-classification's
        // SessionDuration feature reads) actually gets set - previously
        // nothing ever called this and every session's duration was null
        // forever. Only ends the FIRST time the app backgrounds or quits in
        // a given login - a later resume keeps using the same
        // TelemetrySessionId rather than opening a new session, so this
        // deliberately doesn't try to model "session resumed after
        // backgrounding" as a session boundary; that's a bigger design
        // question than fixing the always-null duration.
        private void EndTelemetrySession()
        {
            if (_telemetrySessionEnded || TelemetrySessionId <= 0 || string.IsNullOrEmpty(AccessToken)) return;
            _telemetrySessionEnded = true;

            var resolved = EnvironmentResolver.Resolve("[NAS Telemetry]");
            if (resolved.Settings == null) return;

            var telemetryApi = new TelemetryApi(this, resolved.Settings, resolved.TrustAnyCertificate);
            var request = new EndCustomerSessionRequest
            {
                customerSessionId = TelemetrySessionId,
                endedAt = DateTime.UtcNow.ToString("o")
            };
            telemetryApi.EndSession(request, AccessToken, result =>
            {
                if (!result.Success)
                    Debug.LogWarning($"[NAS Telemetry] Failed to end session: {result.Error.Detail}");
            });
        }

        // Fires once per car selection (the "Start AR" tap), not per
        // carousel swipe - CarSelectionScreenController doesn't currently
        // publish anything on a card merely becoming centered, so this is
        // the earliest real, stable hook for "customer looked at this car
        // with intent" available today. Under-counts casual browsing
        // compared to true per-swipe view tracking; a possible future
        // enhancement, not something already being claimed here.
        private void LogVehicleViewedEvent(VehicleInfo vehicle)
        {
            if (vehicle == null || vehicle.id <= 0) return;
            LogActivityEvent("vehicle_viewed", vehicle.id);
        }

        // Best-effort, same as every other telemetry send: skipped when the
        // session isn't ready, a failure only logs a warning. vehicleId 0 means
        // "no vehicle" (JsonUtility can't send a null int; the backend treats
        // <= 0 as unset).
        private void LogActivityEvent(string eventType, int vehicleId)
        {
            if (TelemetrySessionId <= 0 || string.IsNullOrEmpty(AccessToken)) return;

            var resolved = EnvironmentResolver.Resolve("[NAS Telemetry]");
            if (resolved.Settings == null) return;

            var telemetryApi = new TelemetryApi(this, resolved.Settings, resolved.TrustAnyCertificate);
            var request = new ActivityEventTelemetryRequest
            {
                customerSessionId = TelemetrySessionId,
                clientEventId = Guid.NewGuid().ToString(),
                eventType = eventType,
                occurredAt = DateTime.UtcNow.ToString("o"),
                vehicleModelId = vehicleId
            };
            telemetryApi.LogEvent(request, AccessToken, result =>
            {
                if (!result.Success)
                    Debug.LogWarning($"[NAS Telemetry] {eventType} event failed: {result.Error.Detail}");
            });
        }

        private void InitializeStorage()
        {
            IStorageConfig config = Resources.Load<DevStorageConfig>("Config/DevStorageConfig");
            if (config == null)
            {
                Debug.LogError("DevStorageConfig not found in Resources/Config/!");
                return;
            }

            // _useProduction is currently dead - only affects the log line below, not
            // which service gets constructed. Deliberately not fixed yet (no plan for
            // multiple Tigris environments at this stage) - see .claude/CLAUDE.md's
            // Tigris storage TODO for what to do here once AR integration starts:
            // follow AuthController's single-enum pattern, not a second drifting bool.
            _storage = new DevStorageService(config);

            Debug.Log($"GameManager: Using {(_useProduction ? "PRODUCTION" : "DEVELOPMENT")} storage.");
        }

        // Public storage methods — unchanged.
        public async Task<Result> UploadModel(string localFilePath, string modelKey)
        {
            if (_storage == null) return Result.Failure("Storage not initialized.");
            return await _storage.UploadModelAsync(localFilePath, modelKey);
        }

        public async Task<Result<byte[]>> DownloadModel(string modelKey)
        {
            if (_storage == null) return Result<byte[]>.Failure("Storage not initialized.");
            return await _storage.DownloadModelAsync(modelKey);
        }

        public async Task<Result<List<string>>> ListModels(string prefix = "")
        {
            if (_storage == null) return Result<List<string>>.Failure("Storage not initialized.");
            return await _storage.ListModelsAsync(prefix);
        }

        public async Task<Result> UploadModels(List<string> localFilePaths, List<string> modelKeys)
        {
            if (_storage == null) return Result.Failure("Storage not initialized.");
            return await _storage.UploadModelsAsync(localFilePaths, modelKeys);
        }

        public async Task<Result<List<(string Key, byte[] Data)>>> DownloadModels(List<string> modelKeys)
        {
            if (_storage == null) return Result<List<(string Key, byte[] Data)>>.Failure("Storage not initialized.");
            return await _storage.DownloadModelsAsync(modelKeys);
        }
    }
}
