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
        // The side menu and its screens load from Resources (like the dealership list), so adding them needed no scene edit.
        private const string DrawerUxmlPath = "UI/HamburgerDrawer";
        private const string EditProfileUxmlPath = "UI/EditProfile";
        private const string ChangePasswordUxmlPath = "UI/ChangePassword";
        private const string PrivacyUxmlPath = "UI/Privacy";

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
            EventBus.Subscribe<MenuRequestedEvent>(OnMenuRequested);
            EventBus.Subscribe<EditProfileRequestedEvent>(OnEditProfileRequested);
            EventBus.Subscribe<ChangePasswordRequestedEvent>(OnChangePasswordRequested);
            EventBus.Subscribe<PrivacyRequestedEvent>(OnPrivacyRequested);
            EventBus.Subscribe<CloseProfileScreenRequestedEvent>(OnCloseProfileScreenRequested);
            EventBus.Subscribe<SessionEndedEvent>(OnSessionEnded);
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
            EventBus.Unsubscribe<MenuRequestedEvent>(OnMenuRequested);
            EventBus.Unsubscribe<EditProfileRequestedEvent>(OnEditProfileRequested);
            EventBus.Unsubscribe<ChangePasswordRequestedEvent>(OnChangePasswordRequested);
            EventBus.Unsubscribe<PrivacyRequestedEvent>(OnPrivacyRequested);
            EventBus.Unsubscribe<CloseProfileScreenRequestedEvent>(OnCloseProfileScreenRequested);
            EventBus.Unsubscribe<SessionEndedEvent>(OnSessionEnded);
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

        // ---- Side menu and the screens it opens ---------------------------------------------------
        // The menu is an overlay on top of whatever card is showing (today only Car Selection opens it); the
        // three screens it leads to are ordinary cards, and finishing any of them goes back to Car Selection,
        // where the menu was opened from. Signing out is announced by LogoutRequestedEvent but navigated on
        // SessionEndedEvent, which GameManager publishes only after it has cleared the session.
        private void OnMenuRequested(MenuRequestedEvent evt) => ShowMenuDrawer();
        private void OnEditProfileRequested(EditProfileRequestedEvent evt) => ShowProfileScreen(EditProfileUxmlPath, ScreenNames.EditProfile, typeof(EditProfileController));
        private void OnChangePasswordRequested(ChangePasswordRequestedEvent evt) => ShowProfileScreen(ChangePasswordUxmlPath, ScreenNames.ChangePassword, typeof(ChangePasswordController));
        private void OnPrivacyRequested(PrivacyRequestedEvent evt) => ShowProfileScreen(PrivacyUxmlPath, ScreenNames.Privacy, typeof(PrivacyController));
        private void OnCloseProfileScreenRequested(CloseProfileScreenRequestedEvent evt) => ShowCarSelectionScreen();
        private void OnSessionEnded(SessionEndedEvent evt) => ShowLoginCard();

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

        public void ShowMenuDrawer()
        {
            if (GetComponent<HamburgerDrawerController>() != null) return; // already open
            var uxml = Resources.Load<VisualTreeAsset>(DrawerUxmlPath);
            if (uxml == null)
            {
                Debug.LogError($"Menu UXML not found (Resources/{DrawerUxmlPath}).");
                return;
            }

            // On the document root, above the card container, so it covers the whole screen.
            var overlay = uxml.Instantiate();
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0;
            overlay.style.top = 0;
            overlay.style.right = 0;
            overlay.style.bottom = 0;
            _uiDocument.rootVisualElement.Add(overlay);
            gameObject.AddComponent<HamburgerDrawerController>();
        }

        private void ShowProfileScreen(string uxmlPath, string screenName, System.Type controllerType)
        {
            var uxml = Resources.Load<VisualTreeAsset>(uxmlPath);
            if (uxml == null)
            {
                Debug.LogError($"Screen UXML not found (Resources/{uxmlPath}).");
                return;
            }
            _cardContainer.Clear();
            RemoveCardControllers();
            uxml.CloneTree(_cardContainer);
            gameObject.AddComponent(controllerType);
            EventBus.Publish(new ScreenShownEvent(screenName));
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

            var editProfileCtrl = GetComponent<EditProfileController>();
            if (editProfileCtrl != null) Destroy(editProfileCtrl);

            var changePasswordCtrl = GetComponent<ChangePasswordController>();
            if (changePasswordCtrl != null) Destroy(changePasswordCtrl);

            var privacyCtrl = GetComponent<PrivacyController>();
            if (privacyCtrl != null) Destroy(privacyCtrl);

            // Any screen change closes the side menu (it may have just launched this change).
            var drawerCtrl = GetComponent<HamburgerDrawerController>();
            if (drawerCtrl != null) drawerCtrl.CloseImmediate();
        }

        private void OnDisable()
        {
            UnsubscribeFromFlowEvents();
            RemoveCardControllers();
        }
    }
}
