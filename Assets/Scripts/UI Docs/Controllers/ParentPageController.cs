using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using NAS.Core;
using NAS.Core.Events;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// Owns screen navigation ONLY. It reacts to domain events to decide which
    /// card to show next — it has no idea how login, registration, or auth
    /// validation actually work, and it no longer manually wires callbacks
    /// onto whichever card it just instantiated. Any future system (analytics,
    /// GameManager, a tutorial overlay, etc.) can react to the same events
    /// independently, without ParentPageController knowing they exist.
    /// </summary>
    public class ParentPageController : MonoBehaviour
    {
        private const string ArSceneName = "AR Scene";

        [SerializeField] private VisualTreeAsset _splashCardUxml;
        [SerializeField] private VisualTreeAsset _loginCardUxml;
        [SerializeField] private VisualTreeAsset _registerCardUxml;
        // Optional: falls back to Resources/UI/DealershipSelection.uxml when
        // unassigned, so this screen needed no scene edit to add.
        [SerializeField] private VisualTreeAsset _dealershipSelectionCardUxml;
        [SerializeField] private VisualTreeAsset _carSelectionCardUxml;
        [SerializeField] private VisualTreeAsset _estimatorCardUxml;
        [SerializeField] private VisualTreeAsset _carCardUxml;
        [SerializeField] private string _backgroundImagePath = "Assets/Textures/UI/background.png";

        private UIDocument _uiDocument;
        private VisualElement _cardContainer;

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null)
                _uiDocument = gameObject.AddComponent<UIDocument>();

            var root = _uiDocument.rootVisualElement;
            _cardContainer = root.Q<VisualElement>("center-container");

            SetBackgroundImage(root);
            SubscribeToFlowEvents();
        }

        // DecideInitialScreen() reads GameManager.Instance, which is set in
        // GameManager's own Awake() - Unity doesn't guarantee Awake() order
        // across different GameObjects, so calling this from OnEnable() (which
        // has the same ordering risk as Awake()) can run before GameManager
        // has set Instance, throwing a NullReferenceException. Start() is
        // guaranteed to run only after every object's Awake() has, same fix
        // AuthController already uses for the same reason.
        private void Start()
        {
            // Splash is shown once per app session (GameManager.HasShownSplash),
            // not on every "Main App" scene load - Back/Confirm from the AR scene
            // both reload this scene, and re-showing the splash on every return
            // trip would be annoying, not a "first thing the app shows" beat.
            if (!GameManager.Instance.HasShownSplash)
                ShowSplashCard();
            else
                DecideInitialScreen();
        }

        private void DecideInitialScreen()
        {
            var session = GameManager.Instance;

            if (session.CurrentUser == null)
            {
                ShowLoginCard();
            }
            else if (session.SelectedDealership == null)
            {
                ShowDealershipSelectionScreen();
            }
            else if (session.SelectedCar != null && session.ReturnToEstimator)
            {
                ShowEstimatorCard();
                session.ReturnToEstimator = false;
            }
            else
            {
                ShowCarSelectionScreen();
            }
        }

        private void SubscribeToFlowEvents()
        {
            EventBus.Subscribe<SessionAuthenticatedEvent>(OnAuthSucceeded);
            EventBus.Subscribe<NavigateToRegisterRequestedEvent>(OnNavigateToRegister);
            EventBus.Subscribe<NavigateToLoginRequestedEvent>(OnNavigateToLogin);
            EventBus.Subscribe<SessionDealershipSelectedEvent>(OnDealershipSelected);
            EventBus.Subscribe<ChangeDealershipRequestedEvent>(OnChangeDealershipRequested);
            EventBus.Subscribe<SessionCarSelectedEvent>(OnCarSelected);
            EventBus.Subscribe<SplashDismissedEvent>(OnSplashDismissed);
            EventBus.Subscribe<ExitArRequestedEvent>(OnExitAr);
            EventBus.Subscribe<ReturnToCarSelectionRequestedEvent>(OnReturnToCarSelectionRequested);
        }

        private void UnsubscribeFromFlowEvents()
        {
            EventBus.Unsubscribe<SessionAuthenticatedEvent>(OnAuthSucceeded);
            EventBus.Unsubscribe<NavigateToRegisterRequestedEvent>(OnNavigateToRegister);
            EventBus.Unsubscribe<NavigateToLoginRequestedEvent>(OnNavigateToLogin);
            EventBus.Unsubscribe<SessionDealershipSelectedEvent>(OnDealershipSelected);
            EventBus.Unsubscribe<ChangeDealershipRequestedEvent>(OnChangeDealershipRequested);
            EventBus.Unsubscribe<SessionCarSelectedEvent>(OnCarSelected);
            EventBus.Unsubscribe<SplashDismissedEvent>(OnSplashDismissed);
            EventBus.Unsubscribe<ExitArRequestedEvent>(OnExitAr);
            EventBus.Unsubscribe<ReturnToCarSelectionRequestedEvent>(OnReturnToCarSelectionRequested);
        }

        // Subscribed to GameManager's SessionAuthenticatedEvent/SessionCarSelectedEvent,
        // not the raw AuthSucceededEvent/CarSelectedEvent - GameManager publishes these
        // only after CurrentUser/AccessToken/SelectedCar are already set, so anything
        // downstream of these handlers (e.g. CarSelectionScreenController reading
        // GameManager.AccessToken) is guaranteed to see up-to-date state. See
        // GameEvents.cs's doc comments on both event pairs for why this matters -
        // subscribing to the raw events directly here caused a real bug.
        // Login always leads to "where are you buying from?" - GameManager clears
        // SelectedDealership on every login, so there's nothing to skip past.
        private void OnAuthSucceeded(SessionAuthenticatedEvent evt) => ShowDealershipSelectionScreen();
        private void OnDealershipSelected(SessionDealershipSelectedEvent evt) => ShowCarSelectionScreen();
        private void OnChangeDealershipRequested(ChangeDealershipRequestedEvent evt) => ShowDealershipSelectionScreen();
        private void OnNavigateToRegister(NavigateToRegisterRequestedEvent evt) => ShowRegisterCard();
        private void OnNavigateToLogin(NavigateToLoginRequestedEvent evt) => ShowLoginCard();
        // AR Scene is loaded additively exactly once per app session
        // (GameManager.IsArSceneLoaded) and never reloaded after that -
        // destroying and recreating ARSession/XROrigin via a full scene
        // reload on every AR visit was causing a black camera feed on the
        // second+ entry (not the documented AR Foundation lifecycle; see
        // EnterArRequestedEvent/ExitArRequestedEvent's doc comments in
        // GameEvents.cs). First visit loads the scene; every visit after
        // that just hides this screen's own UI and tells the already-loaded
        // AR scene to show itself again via EnterArRequestedEvent.
        // ArViewportController owns the Back/Confirm buttons that publish
        // ExitArRequestedEvent to bring the user back here - see OnExitAr().
        private void OnCarSelected(SessionCarSelectedEvent evt)
        {
            // Published before HideUi()/the scene swap below, not after - the
            // loading overlay lives on the persistent Game Manager and
            // renders above any scene, so showing it first covers the gap
            // between this screen's UI disappearing and the AR scene/camera
            // actually being ready (previously a brief flash of the raw
            // scene skybox). SelectedCarModelLoader fires its own
            // LoadingStartedEvent moments later once it starts the actual
            // download - harmless/idempotent, and its LoadingFinishedEvent
            // is still the one that clears this.
            EventBus.Publish(new LoadingStartedEvent("Entering AR..."));
            HideUi();
            EventBus.Publish(new ScreenShownEvent(ScreenNames.ArViewport));
            if (!GameManager.Instance.IsArSceneLoaded)
            {
                GameManager.Instance.IsArSceneLoaded = true;
                SceneManager.LoadScene(ArSceneName, LoadSceneMode.Additive);
            }
            else
            {
                EventBus.Publish(new EnterArRequestedEvent());
            }
        }

        private void OnSplashDismissed(SplashDismissedEvent evt)
        {
            GameManager.Instance.HasShownSplash = true;
            DecideInitialScreen();
        }

        // Raised by EstimateConfirmationOverlayController's "Back to Car
        // Selection" button once a submission has succeeded - reuses the
        // existing ShowCarSelectionScreen() card swap, same as every other
        // navigation this router handles.
        private void OnReturnToCarSelectionRequested(ReturnToCarSelectionRequestedEvent evt) => ShowCarSelectionScreen();

        // AR Scene is never unloaded (see OnCarSelected above), so this is
        // the only way back to a Main App card after the first AR visit -
        // Start() only ever runs once now, it won't fire again on return.
        private void OnExitAr(ExitArRequestedEvent evt)
        {
            ShowUi();
            DecideInitialScreen();
        }

        private void HideUi() => _uiDocument.rootVisualElement.style.display = DisplayStyle.None;
        private void ShowUi() => _uiDocument.rootVisualElement.style.display = DisplayStyle.Flex;

        private void SetBackgroundImage(VisualElement root)
        {
            var backgroundImage = root.Q<Image>("background-image");
            if (backgroundImage != null && !string.IsNullOrEmpty(_backgroundImagePath))
            {
                var texture = Resources.Load<Texture2D>(_backgroundImagePath);
                if (texture != null)
                    backgroundImage.image = texture;
                else
                    Debug.LogWarning($"Background image not found at: {_backgroundImagePath}");
            }
        }

        public void ShowSplashCard()
        {
            if (_splashCardUxml == null)
            {
                // No splash UXML assigned in the Inspector - fall back to the
                // pre-splash behavior rather than showing a blank screen.
                DecideInitialScreen();
                return;
            }
            _cardContainer.Clear();
            RemoveCardControllers();
            _splashCardUxml.CloneTree(_cardContainer);
            gameObject.AddComponent<SplashScreenController>();
            EventBus.Publish(new ScreenShownEvent(ScreenNames.Splash));
        }

        public void ShowLoginCard()
        {
            if (_loginCardUxml == null) return;
            _cardContainer.Clear();
            RemoveCardControllers();
            _loginCardUxml.CloneTree(_cardContainer);
            gameObject.AddComponent<LoginCardController>();
            EventBus.Publish(new ScreenShownEvent(ScreenNames.Login));
        }

        public void ShowRegisterCard()
        {
            if (_registerCardUxml == null) return;
            _cardContainer.Clear();
            RemoveCardControllers();
            _registerCardUxml.CloneTree(_cardContainer);
            gameObject.AddComponent<RegisterCardController>();
            EventBus.Publish(new ScreenShownEvent(ScreenNames.Register));
        }

        public void ShowDealershipSelectionScreen()
        {
            var uxml = _dealershipSelectionCardUxml != null
                ? _dealershipSelectionCardUxml
                : Resources.Load<VisualTreeAsset>("UI/DealershipSelection");
            if (uxml == null)
            {
                Debug.LogError("Dealership selection UXML not found (Resources/UI/DealershipSelection).");
                return;
            }
            _cardContainer.Clear();
            RemoveCardControllers();
            uxml.CloneTree(_cardContainer);
            gameObject.AddComponent<DealershipSelectionController>();
            EventBus.Publish(new ScreenShownEvent(ScreenNames.DealershipSelection));
        }

        public void ShowCarSelectionScreen()
        {
            if (_carSelectionCardUxml == null) return;
            _cardContainer.Clear();
            RemoveCardControllers();
            _carSelectionCardUxml.CloneTree(_cardContainer);
            var carSelectionCtrl = gameObject.AddComponent<CarSelectionScreenController>();
            carSelectionCtrl.Initialize(_carCardUxml);
            EventBus.Publish(new ScreenShownEvent(ScreenNames.CarSelection));
        }

        public void ShowEstimatorCard()
        {
            if (_estimatorCardUxml == null) return;
            _cardContainer.Clear();
            RemoveCardControllers();
            _estimatorCardUxml.CloneTree(_cardContainer);
            gameObject.AddComponent<EstimatorCardController>();
            EventBus.Publish(new ScreenShownEvent(ScreenNames.Estimator));
        }

        private void RemoveCardControllers()
        {
            var splashCtrl = GetComponent<SplashScreenController>();
            if (splashCtrl != null) Destroy(splashCtrl);

            var loginCtrl = GetComponent<LoginCardController>();
            if (loginCtrl != null) Destroy(loginCtrl);

            var registerCtrl = GetComponent<RegisterCardController>();
            if (registerCtrl != null) Destroy(registerCtrl);

            var dealershipCtrl = GetComponent<DealershipSelectionController>();
            if (dealershipCtrl != null) Destroy(dealershipCtrl);

            var carSelectionCtrl = GetComponent<CarSelectionScreenController>();
            if (carSelectionCtrl != null) Destroy(carSelectionCtrl);

            var estimatorCtrl = GetComponent<EstimatorCardController>();
            if (estimatorCtrl != null) Destroy(estimatorCtrl);
        }

        private void OnDisable()
        {
            UnsubscribeFromFlowEvents();
            RemoveCardControllers();
        }
    }
}
