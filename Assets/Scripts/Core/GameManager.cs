using NAS.Core.Interfaces;
using NAS.Core.Events;
using NAS.Core.Networking;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NAS.Core.Models;
using UnityEngine;

namespace NAS.Core
{
    /// <summary>
    /// Holds session state and owns the storage service. CurrentUser/SelectedCar are derived by
    /// listening to domain events rather than being set by whichever controller handles a click.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Environment")]
        [Tooltip("Backend for the editor and development builds. A release build always uses Staging. Addresses are in Resources/Config. The committed value is Staging; switch it locally to test against your machine and do not commit that.")]
        [SerializeField] private AppEnvironment _environment = AppEnvironment.Local;
        // Not always the dropdown: a release build ignores it (see EnvironmentPolicy).
        public AppEnvironment CurrentEnvironment => EnvironmentPolicy.Effective(_environment, Debug.isDebugBuild);

        [Header("Session")]
        public User CurrentUser { get; private set; }
        public string AccessToken { get; private set; }
        public VehicleInfo SelectedCar { get; private set; }
        // Chosen fresh every login; null until the customer picks one.
        public DealershipInfo SelectedDealership { get; private set; }
        // Only used to highlight the last choice in the list; never auto-selected.
        private const string LastDealershipIdPrefKey = "NAS.LastDealershipId";
        public int LastDealershipId => PlayerPrefs.GetInt(LastDealershipIdPrefKey, 0);
        public bool ReturnToEstimator { get; set; } = false;
        // Lives here, not on ParentPageController, because that is recreated on every Main App load.
        public bool HasShownSplash { get; set; } = false;
        public bool RestoringSession { get; set; } = false;
        // The AR scene is loaded once and never unloaded: re-entering AR toggles it, because recreating
        // ARSession/XROrigin caused a black camera feed on the second entry.
        public bool IsArSceneLoaded { get; set; } = false;
        // Set after Confirm saves the configuration; 0 means unset (JsonUtility cannot send a null int).
        public int SelectedConfigurationId { get; set; } = 0;
        public CustomizationSelection Customization { get; } = new CustomizationSelection();
        // From POST /api/telemetry/session; 0 means not started or failed. Every other telemetry call needs it.
        public int TelemetrySessionId { get; set; } = 0;
        // Backgrounding fires pause many times and quit can follow; only the first counts as the end.
        private bool _telemetrySessionEnded = false;
        // True only if the selected car's own model loaded (not the placeholder fallback). A visit spent on a
        // load failure is not real placement behaviour, so ObjectPlacerController skips its AR telemetry.
        public bool CurrentArModelLoadSucceeded { get; set; } = false;

        private const long ModelCacheBytes = 500L * 1024 * 1024;
        private ModelDownloader _modelDownloader;

        // Visits that finish before the telemetry session exists (splash, login, register) wait in _pendingVisits.
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
            gameObject.AddComponent<AccentThemeRunner>();

            _modelDownloader = new ModelDownloader(this, new ModelDiskCache(System.IO.Path.Combine(Application.persistentDataPath, "models"), ModelCacheBytes));
            _screens = new ScreenVisitTracker(OnScreenVisitFinished);

            // Subscribed in Awake, not OnEnable, so GameManager is ready before anything else in the scene.
            // (Session*Event is the real fix for subscribers reading state before it is set.)
            EventBus.Subscribe<AuthSucceededEvent>(OnAuthSucceeded);
            EventBus.Subscribe<ScreenShownEvent>(OnScreenShown);
            EventBus.Subscribe<DealershipSelectedEvent>(OnDealershipSelected);
            EventBus.Subscribe<ArModelLoadFailedEvent>(OnArModelLoadFailed);
            EventBus.Subscribe<CarSelectedEvent>(OnCarSelected);
            EventBus.Subscribe<ReturnToEstimatorRequestedEvent>(OnReturnToEstimatorRequested);
            EventBus.Subscribe<LogoutRequestedEvent>(OnLogoutRequested);
            EventBus.Subscribe<ProfileUpdatedEvent>(OnProfileUpdated);
            EventBus.Subscribe<AccessTokenRefreshedEvent>(OnAccessTokenRefreshed);
            ApiClient.SessionRejected += OnSessionRejected;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<AuthSucceededEvent>(OnAuthSucceeded);
            EventBus.Unsubscribe<ScreenShownEvent>(OnScreenShown);
            EventBus.Unsubscribe<DealershipSelectedEvent>(OnDealershipSelected);
            EventBus.Unsubscribe<ArModelLoadFailedEvent>(OnArModelLoadFailed);
            EventBus.Unsubscribe<CarSelectedEvent>(OnCarSelected);
            EventBus.Unsubscribe<ReturnToEstimatorRequestedEvent>(OnReturnToEstimatorRequested);
            EventBus.Unsubscribe<LogoutRequestedEvent>(OnLogoutRequested);
            EventBus.Unsubscribe<ProfileUpdatedEvent>(OnProfileUpdated);
            EventBus.Unsubscribe<AccessTokenRefreshedEvent>(OnAccessTokenRefreshed);
            ApiClient.SessionRejected -= OnSessionRejected;
        }

        // Pause is the reliable end-of-session signal on mobile; quit is a fallback (e.g. stopping Play mode).
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

        // The Session*Event is published after the fields are set so no subscriber can see it early. Do not reorder.
        private void OnAuthSucceeded(AuthSucceededEvent evt)
        {
            CurrentUser = evt.User;
            AccessToken = evt.AccessToken;
            SelectedDealership = null; // chosen fresh every login
            EventBus.Publish(new SessionAuthenticatedEvent(CurrentUser, AccessToken));
            StartTelemetrySession();
        }

        private void OnScreenShown(ScreenShownEvent evt) => _screens?.Show(evt.ScreenName);

        private void OnSessionRejected()
        {
            if (string.IsNullOrEmpty(AccessToken)) return;
            EventBus.Publish(new AccessTokenRejectedEvent());
        }

        private void OnAccessTokenRefreshed(AccessTokenRefreshedEvent evt)
        {
            if (CurrentUser != null) AccessToken = evt.AccessToken;
        }

        // Close the screen visit and telemetry session first: they belong to this login and
        // need its token and session id. SessionEndedEvent goes out only after the state is cleared.
        private void OnLogoutRequested(LogoutRequestedEvent evt)
        {
            _screens?.Stop();
            EndTelemetrySession();

            CurrentUser = null;
            AccessToken = null;
            SelectedCar = null;
            SelectedDealership = null;
            SelectedConfigurationId = 0;
            Customization.Clear();
            ReturnToEstimator = false;
            TelemetrySessionId = 0;
            _telemetrySessionEnded = false; // the next login opens a new session that can be ended again
            _pendingVisits.Clear();         // visits waiting for a session id belonged to the one that just ended

            EventBus.Publish(new SessionEndedEvent());
        }

        // Keeps the name in the menu header current after a profile edit.
        private void OnProfileUpdated(ProfileUpdatedEvent evt)
        {
            if (CurrentUser == null) return;
            CurrentUser.displayName = $"{evt.FirstName} {evt.LastName}".Trim();
        }

        // Sent now if the session exists, otherwise held (capped) until StartTelemetrySession gets its id.
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

            // A car or configuration from another dealership's catalog must not carry over.
            var changed = SelectedDealership != null && SelectedDealership.id != dealership.id;
            if (SelectedDealership == null || changed)
            {
                SelectedCar = null;
                SelectedConfigurationId = 0;
                Customization.Clear();
            }
            SelectedDealership = dealership;
            PlayerPrefs.SetInt(LastDealershipIdPrefKey, dealership.id);
            EventBus.Publish(new SessionDealershipSelectedEvent(dealership));
            ApplyDealershipToTelemetrySession();
            // Only a switch is logged; the first choice is not a signal.
            if (changed)
                LogActivityEvent("dealership_changed", 0);
        }

        // Explains an abandoned AR visit: the customer was looking at a placeholder.
        private void OnArModelLoadFailed(ArModelLoadFailedEvent evt) =>
            LogActivityEvent("ar_load_failed", SelectedCar != null ? SelectedCar.id : 0);

        // Idempotent. If the session has no id yet, StartTelemetrySession calls this once it does.
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
            Customization.Clear();
            EventBus.Publish(new SessionCarSelectedEvent(SelectedCar));
            LogVehicleViewedEvent(SelectedCar);
        }
        private void OnReturnToEstimatorRequested(ReturnToEstimatorRequestedEvent evt) => ReturnToEstimator = true;

        // Best-effort: a failure leaves TelemetrySessionId at 0, which every other telemetry call treats as "skip".
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
                    // The dealership may have been picked before this returned.
                    ApplyDealershipToTelemetrySession();
                    // Screens already left (splash, login) were waiting for this id.
                    FlushPendingScreenVisits();
                }
                else
                    Debug.LogWarning($"[NAS Telemetry] Failed to start session: {result.Error.Detail}");
            });
        }

        // Sets EndedAt/DurationSeconds, which buyer classification reads. Ends only once per login: a later
        // resume keeps the same session id rather than opening a new one.
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

        // Fires on car selection ("Start AR"), the earliest stable signal of intent; swiping is not tracked.
        private void LogVehicleViewedEvent(VehicleInfo vehicle)
        {
            if (vehicle == null || vehicle.id <= 0) return;
            LogActivityEvent("vehicle_viewed", vehicle.id);
        }

        // vehicleId 0 means no vehicle (JsonUtility cannot send a null int).
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

        // The API signs a short-lived link for the model; the app holds no storage credentials (see ModelDownloader).
        public Task<Result<byte[]>> DownloadModel(int vehicleId, string modelKey)
        {
            if (string.IsNullOrEmpty(AccessToken)) return Task.FromResult(Result<byte[]>.Failure("Not signed in."));
            return _modelDownloader.DownloadAsync(vehicleId, modelKey, AccessToken);
        }
    }
}
