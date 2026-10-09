using System;
using System.Collections.Generic;
using NAS.Core;
using NAS.Core.Events;
using NAS.Core.Models;
using NAS.Core.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// Top bar + Customize sheet for the AR viewport scene (AR Scene.unity).
    /// Pre-placed on a scene GameObject with its own UIDocument - unlike the
    /// Main-App-scene card controllers, this isn't added dynamically via
    /// AddComponent, since this scene only ever shows one screen and has no
    /// router.
    /// </summary>
    public class ArViewportController : MonoBehaviour
    {
        private const string PaintCategoryId = "paint";
        private const string LogPrefix = "[NAS AR Viewport]";

        private static readonly string[] CategoryIds = { "wheel", "paint", "trims", "dashboard" };
        private static readonly string[] CategoryButtonNames = { "category-wheel", "category-paint", "category-trims", "category-dashboard" };

        private Button _backButton;
        private Button _confirmButton;
        private Label _carNameLabel;

        private Button _settingsButton;
        // "Request test drive" - tells the dealer this customer wants one
        // (an activity event + a lead the salesperson can see). Remembers
        // which car it was sent for, so a second tap on the same car just
        // shows the confirmed state instead of sending again.
        private Button _testDriveButton;
        private int _testDriveRequestedForCarId;
        private bool _isRequestingTestDrive;
        private IEstimatorApi _estimatorApi;
        private Button _sheetCloseButton;
        private VisualElement _sheetBackdrop;
        private VisualElement _customizeSheet;

        // Rotation slider - replaces the old two-finger twist gesture (real
        // users found twisting hard to do accurately). Owns the slider
        // control itself; CarManipulationController applies the values via
        // RotationSliderChangedEvent, this controller never touches the
        // placed car directly.
        private Slider _rotationSlider;
        private Label _scaleHintLabel;
        private VisualElement _rotationSliderRow;

        // Vertical-offset slider - a manual correction for when the anchor's
        // tracked height is visibly off (see CLAUDE.md's AR viewport section
        // on anchor stability). Same ownership split as the rotation slider:
        // this controller owns the control, CarManipulationController
        // applies the values via VerticalOffsetSliderChangedEvent.
        private Slider _verticalOffsetSlider;
        private VisualElement _verticalOffsetSliderColumn;
        private Button _resetPositionButton;

        // Hidden until a car actually exists in the scene - showing a
        // rotation/height slider and a Customize button with nothing yet to
        // manipulate is confusing. Shown once CarPlacedEvent fires.
        private VisualElement _settingsButtonRow;

        // Wheel/Trims/Dashboard have no data model to back real customization
        // yet (see .claude/CLAUDE.md's "Vehicle catalog" section) - tapping
        // them just shows the same "Coming soon" placeholder the whole sheet
        // used to show. Paint is real: backed by CarData.exteriorColors,
        // fetched from the customer vehicle API.
        private readonly Dictionary<string, Button> _categoryButtons = new Dictionary<string, Button>();
        private VisualElement _swatchRow;
        private Label _categoryPlaceholderText;
        private Button _selectedSwatch;
        private int _selectedColorOptionId;
        private string _activeCategoryId;

        private IConfigurationApi _configurationApi;
        private bool _isConfirming;

        // VehicleInteraction telemetry - paint is currently the only real
        // customization surface (see the Dictionary comment above), so this
        // is the sole source of colour_changes telemetry today. Reset per AR
        // visit in ShowForCurrentCar(), sent once when the customer leaves AR
        // (back or confirm) in SendVehicleInteractionTelemetry().
        private int _colourChangeCount;
        private string _clientVehicleInteractionId;
        private DateTime _vehicleInteractionStartedAt;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
                return;

            // Unity doesn't guarantee OnEnable order across components on the same
            // GameObject - UIDocument's own OnEnable (which builds rootVisualElement)
            // may not have run yet when this one fires. Same class of gotcha as the
            // documented AddComponent<T>()/OnEnable ordering issue elsewhere in this
            // project - poll for a frame instead of assuming it's ready synchronously.
            var root = uiDocument.rootVisualElement;
            if (root == null)
            {
                StartCoroutine(WaitForRootThenBind(uiDocument));
                return;
            }

            BindUi(root);
        }

        private System.Collections.IEnumerator WaitForRootThenBind(UIDocument uiDocument)
        {
            while (uiDocument.rootVisualElement == null)
                yield return null;

            BindUi(uiDocument.rootVisualElement);
        }

        private VisualElement _root;

        private void BindUi(VisualElement root)
        {
            _root = root;
            _backButton = root.Q<Button>("back-button");
            _confirmButton = root.Q<Button>("confirm-button");
            _carNameLabel = root.Q<Label>("car-name-label");
            _settingsButton = root.Q<Button>("settings-button");
            _testDriveButton = root.Q<Button>("test-drive-button");
            _sheetCloseButton = root.Q<Button>("sheet-close-button");
            _sheetBackdrop = root.Q<VisualElement>("sheet-backdrop");
            _customizeSheet = root.Q<VisualElement>("customize-sheet");
            _swatchRow = root.Q<VisualElement>("swatch-row");
            _categoryPlaceholderText = root.Q<Label>("category-placeholder-text");
            _rotationSlider = root.Q<Slider>("rotation-slider");
            _rotationSliderRow = root.Q<VisualElement>("rotation-slider-row");
            _scaleHintLabel = root.Q<Label>("scale-hint-label");
            _verticalOffsetSlider = root.Q<Slider>("vertical-offset-slider");
            _verticalOffsetSliderColumn = root.Q<VisualElement>("vertical-offset-slider-column");
            _resetPositionButton = root.Q<Button>("reset-position-button");
            _settingsButtonRow = root.Q<VisualElement>("settings-button-row");

            if (_rotationSlider != null)
            {
                _rotationSlider.RegisterCallback<PointerDownEvent>(OnRotationSliderGrabbed);
                _rotationSlider.RegisterValueChangedCallback(OnRotationSliderValueChanged);
                _rotationSlider.RegisterCallback<PointerUpEvent>(OnRotationSliderReleased);
            }

            if (_verticalOffsetSlider != null)
            {
                _verticalOffsetSlider.RegisterCallback<PointerDownEvent>(OnVerticalOffsetSliderGrabbed);
                _verticalOffsetSlider.RegisterValueChangedCallback(OnVerticalOffsetSliderValueChanged);
                _verticalOffsetSlider.RegisterCallback<PointerUpEvent>(OnVerticalOffsetSliderReleased);
            }

            if (_backButton != null)
                _backButton.clicked += OnBackClicked;
            if (_confirmButton != null)
                _confirmButton.clicked += OnConfirmClicked;
            if (_settingsButton != null)
                _settingsButton.clicked += OnSettingsClicked;
            if (_testDriveButton != null)
                _testDriveButton.clicked += OnTestDriveClicked;
            if (_resetPositionButton != null)
                _resetPositionButton.clicked += OnResetPositionClicked;
            if (_sheetCloseButton != null)
                _sheetCloseButton.clicked += OnCloseSheetClicked;
            if (_sheetBackdrop != null)
                _sheetBackdrop.RegisterCallback<ClickEvent>(OnBackdropClicked);

            for (var i = 0; i < CategoryIds.Length; i++)
            {
                var button = root.Q<Button>(CategoryButtonNames[i]);
                if (button == null) continue;
                var categoryId = CategoryIds[i];
                _categoryButtons[categoryId] = button;
                button.clicked += () => OnCategoryClicked(categoryId);
            }

            EventBus.Subscribe<EnterArRequestedEvent>(OnEnterAr);
            EventBus.Subscribe<CarScaleChangedEvent>(OnCarScaleChanged);
            EventBus.Subscribe<CarPlacedEvent>(OnCarPlaced);
            ShowForCurrentCar();
        }

        // AR Scene is loaded once and never reloaded (see ParentPageController.OnCarSelected) -
        // this used to be inline in BindUi() since that only ever ran once per
        // visit under the old reload-every-time design. Now it's the thing
        // that refreshes per-visit state (car name, a clean Customize sheet,
        // no stale swatch selection from whatever car was showing last time),
        // called both on the first bind above and on every EnterArRequestedEvent
        // after that.
        private void ShowForCurrentCar()
        {
            ShowUi();

            var selectedCar = GameManager.Instance != null ? GameManager.Instance.SelectedCar : null;
            if (_carNameLabel != null)
                _carNameLabel.text = selectedCar != null ? selectedCar.modelName : string.Empty;

            SetCustomizeSheetOpen(false);
            _activeCategoryId = null;
            foreach (var pair in _categoryButtons)
                pair.Value.RemoveFromClassList("category-button--active");
            _selectedSwatch = null;
            _selectedColorOptionId = 0;
            if (_swatchRow != null)
                _swatchRow.Clear();
            SetElementVisible(_categoryPlaceholderText, false);

            // SetValueWithoutNotify - a plain `.value = 0` would fire
            // RotationSliderChangedEvent through the normal callback path,
            // which is fine in itself (CarManipulationController would just
            // reset the not-yet-placed car's rotation to 0, a no-op), but
            // relying on that side effect to reset the label/slider in sync
            // is more fragile than just doing it directly here.
            _rotationSlider?.SetValueWithoutNotify(0f);
            _verticalOffsetSlider?.SetValueWithoutNotify(0f);
            if (_scaleHintLabel != null)
                _scaleHintLabel.text = "Pinch to scale [1:1]";

            // Hidden again on every fresh AR entry until CarPlacedEvent fires
            // for whatever gets placed this visit - showing controls for a
            // car that isn't in the scene yet is confusing.
            SetElementVisible(_rotationSliderRow, false);
            SetElementVisible(_verticalOffsetSliderColumn, false);
            SetElementVisible(_settingsButtonRow, false);
            RefreshTestDriveButton();

            _colourChangeCount = 0;
            _clientVehicleInteractionId = Guid.NewGuid().ToString();
            _vehicleInteractionStartedAt = DateTime.UtcNow;
        }

        private void OnEnterAr(EnterArRequestedEvent evt) => ShowForCurrentCar();

        private void OnRotationSliderGrabbed(PointerDownEvent evt) =>
            EventBus.Publish(new RotationSliderGrabbedEvent());

        private void OnRotationSliderValueChanged(ChangeEvent<float> evt) =>
            EventBus.Publish(new RotationSliderChangedEvent(evt.newValue));

        private void OnRotationSliderReleased(PointerUpEvent evt) =>
            EventBus.Publish(new RotationSliderReleasedEvent());

        private void OnVerticalOffsetSliderGrabbed(PointerDownEvent evt) =>
            EventBus.Publish(new VerticalOffsetSliderGrabbedEvent());

        private void OnVerticalOffsetSliderValueChanged(ChangeEvent<float> evt) =>
            EventBus.Publish(new VerticalOffsetSliderChangedEvent(evt.newValue));

        private void OnVerticalOffsetSliderReleased(PointerUpEvent evt) =>
            EventBus.Publish(new VerticalOffsetSliderReleasedEvent());

        // Only a car actually being in the scene makes these controls
        // meaningful - see ShowForCurrentCar for where they're hidden again
        // on the next AR entry.
        private void OnCarPlaced(CarPlacedEvent evt)
        {
            SetElementVisible(_rotationSliderRow, true);
            SetElementVisible(_verticalOffsetSliderColumn, true);
            SetElementVisible(_settingsButtonRow, true);
        }

        private void RefreshTestDriveButton()
        {
            if (_testDriveButton == null) return;
            var selectedCar = GameManager.Instance != null ? GameManager.Instance.SelectedCar : null;
            var done = selectedCar != null && selectedCar.id > 0 && selectedCar.id == _testDriveRequestedForCarId;
            _testDriveButton.text = done ? "Test drive requested \u2713" : "Request test drive";
            _testDriveButton.EnableInClassList("test-drive-button--done", done);
            _testDriveButton.SetEnabled(!done && !_isRequestingTestDrive);
        }

        // Best-effort like the rest of the AR scene's network calls: a failure
        // just leaves the button tappable so the customer can try again.
        private void OnTestDriveClicked()
        {
            if (_isRequestingTestDrive) return;

            var gameManager = GameManager.Instance;
            var selectedCar = gameManager != null ? gameManager.SelectedCar : null;
            var accessToken = gameManager != null ? gameManager.AccessToken : null;
            if (selectedCar == null || selectedCar.id <= 0 || string.IsNullOrEmpty(accessToken)
                || gameManager.TelemetrySessionId <= 0)
            {
                Debug.LogWarning($"{LogPrefix} Can't request a test drive yet - not signed in, no car selected, or no session started.");
                return;
            }

            var resolved = EnvironmentResolver.Resolve(LogPrefix);
            if (resolved.Settings == null) return;

            _isRequestingTestDrive = true;
            RefreshTestDriveButton();

            _estimatorApi = new EstimatorApi(this, resolved.Settings, resolved.TrustAnyCertificate);
            var carId = selectedCar.id;
            var request = new RequestTestDriveRequest
            {
                vehicleModelId = carId,
                customerSessionId = gameManager.TelemetrySessionId,
                clientEventId = Guid.NewGuid().ToString(),
                savedConfigurationId = gameManager.SelectedConfigurationId
            };
            _estimatorApi.RequestTestDrive(request, accessToken, result =>
            {
                _isRequestingTestDrive = false;
                if (result.Success)
                    _testDriveRequestedForCarId = carId;
                else
                    Debug.LogWarning($"{LogPrefix} Test drive request failed: {result.Error.Detail}");
                RefreshTestDriveButton();
            });
        }

        // Resets both the underlying car (via CarManipulationController,
        // which owns the actual transform) and this controller's own slider
        // displays, so the vertical slider doesn't sit at a stale value that
        // no longer matches where the car actually is.
        private void OnResetPositionClicked()
        {
            EventBus.Publish(new CarPositionResetRequestedEvent());
            _verticalOffsetSlider?.SetValueWithoutNotify(0f);
        }

        private void OnCarScaleChanged(CarScaleChangedEvent evt)
        {
            if (_scaleHintLabel != null)
                _scaleHintLabel.text = $"Pinch to scale [1:{evt.Multiplier:0.##}]";
        }

        private void ShowUi()
        {
            if (_root != null)
                _root.style.display = DisplayStyle.Flex;
        }

        private void HideUi()
        {
            if (_root != null)
                _root.style.display = DisplayStyle.None;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnterArRequestedEvent>(OnEnterAr);
            EventBus.Unsubscribe<CarScaleChangedEvent>(OnCarScaleChanged);
            EventBus.Unsubscribe<CarPlacedEvent>(OnCarPlaced);

            if (_backButton != null)
                _backButton.clicked -= OnBackClicked;
            if (_confirmButton != null)
                _confirmButton.clicked -= OnConfirmClicked;
            if (_settingsButton != null)
                _settingsButton.clicked -= OnSettingsClicked;
            if (_testDriveButton != null)
                _testDriveButton.clicked -= OnTestDriveClicked;
            if (_resetPositionButton != null)
                _resetPositionButton.clicked -= OnResetPositionClicked;
            if (_sheetCloseButton != null)
                _sheetCloseButton.clicked -= OnCloseSheetClicked;
            if (_sheetBackdrop != null)
                _sheetBackdrop.UnregisterCallback<ClickEvent>(OnBackdropClicked);
            if (_rotationSlider != null)
            {
                _rotationSlider.UnregisterCallback<PointerDownEvent>(OnRotationSliderGrabbed);
                _rotationSlider.UnregisterValueChangedCallback(OnRotationSliderValueChanged);
                _rotationSlider.UnregisterCallback<PointerUpEvent>(OnRotationSliderReleased);
            }
            if (_verticalOffsetSlider != null)
            {
                _verticalOffsetSlider.UnregisterCallback<PointerDownEvent>(OnVerticalOffsetSliderGrabbed);
                _verticalOffsetSlider.UnregisterValueChangedCallback(OnVerticalOffsetSliderValueChanged);
                _verticalOffsetSlider.UnregisterCallback<PointerUpEvent>(OnVerticalOffsetSliderReleased);
            }
        }

        private void OnSettingsClicked() => SetCustomizeSheetOpen(true);
        private void OnCloseSheetClicked() => SetCustomizeSheetOpen(false);
        private void OnBackdropClicked(ClickEvent evt) => SetCustomizeSheetOpen(false);

        private void SetCustomizeSheetOpen(bool open)
        {
            var display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (_sheetBackdrop != null)
                _sheetBackdrop.style.display = display;
            if (_customizeSheet != null)
                _customizeSheet.style.display = display;
            EventBus.Publish(new CustomizeSheetToggledEvent(open));
        }

        private void OnCategoryClicked(string categoryId)
        {
            _activeCategoryId = categoryId;

            foreach (var pair in _categoryButtons)
                pair.Value.EnableInClassList("category-button--active", pair.Key == categoryId);

            if (categoryId == PaintCategoryId)
            {
                ShowPaintSwatches();
            }
            else
            {
                SetElementVisible(_swatchRow, false);
                SetElementVisible(_categoryPlaceholderText, true);
            }
        }

        private void ShowPaintSwatches()
        {
            SetElementVisible(_categoryPlaceholderText, false);

            var selectedCar = GameManager.Instance != null ? GameManager.Instance.SelectedCar : null;
            var colors = selectedCar != null ? selectedCar.exteriorColors : null;

            if (_swatchRow == null)
                return;

            _swatchRow.Clear();
            _selectedSwatch = null;

            if (colors == null || colors.Count == 0)
            {
                // No color data for this car - fall back to the same
                // placeholder the other categories use rather than showing an
                // empty row.
                SetElementVisible(_swatchRow, false);
                SetElementVisible(_categoryPlaceholderText, true);
                return;
            }

            SetElementVisible(_swatchRow, true);

            foreach (var option in colors)
            {
                var swatch = new Button { name = $"swatch-{option.id}" };
                swatch.AddToClassList("color-swatch");

                if (ColorUtility.TryParseHtmlString(option.hexCode, out var color))
                    swatch.style.backgroundColor = new StyleColor(color);

                swatch.clicked += () => OnSwatchClicked(swatch, option);
                _swatchRow.Add(swatch);
            }
        }

        private void OnSwatchClicked(Button swatch, CarColorOption option)
        {
            if (_selectedSwatch != null)
                _selectedSwatch.RemoveFromClassList("color-swatch--selected");

            swatch.AddToClassList("color-swatch--selected");
            _selectedSwatch = swatch;
            _selectedColorOptionId = option.id;
            _colourChangeCount++;

            EventBus.Publish(new PaintColorSelectedEvent(option.hexCode));
        }

        private static void SetElementVisible(VisualElement element, bool visible)
        {
            if (element != null)
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Leaves GameManager.ReturnToEstimator at its default (false).
        // ParentPageController.OnExitAr() -> DecideInitialScreen() sees
        // CurrentUser+SelectedCar already set and ReturnToEstimator false, so
        // it reopens car selection - restored to the previously selected car
        // via CarSelectionScreenController's index-restore logic. Neither
        // scene reloads anymore (see GameEvents.cs's ExitArRequestedEvent doc
        // comment for why) - HideUi() just hides this screen, ParentPageController
        // shows its own UI back in response to the event.
        private void OnBackClicked()
        {
            SendVehicleInteractionTelemetry();
            HideUi();
            EventBus.Publish(new ExitArRequestedEvent());
        }

        // Finalizes a SavedConfiguration for the selected car before handing
        // off to the Estimator - best-effort, same philosophy as
        // EstimatorService's auto-scoring hook. Sends the actually-selected
        // paint swatch (_selectedColorOptionId, 0 if the customer never
        // touched Customize); trim/interior/wheel still have no picker UI, so
        // the backend fills those three in with that vehicle's default
        // options. Gives the eventual Lead a real SavedConfigurationId
        // instead of always leaving it null. Any failure (no network, no
        // vehicle selected, not signed in) must never block the customer from
        // reaching the Estimator - it just proceeds with SelectedConfigurationId
        // left at 0, same as before this existed.
        private void OnConfirmClicked()
        {
            if (_isConfirming) return;

            var gameManager = GameManager.Instance;
            var selectedCar = gameManager != null ? gameManager.SelectedCar : null;
            var accessToken = gameManager != null ? gameManager.AccessToken : null;
            if (selectedCar == null || selectedCar.id <= 0 || string.IsNullOrEmpty(accessToken))
            {
                ProceedToEstimator();
                return;
            }

            var resolved = EnvironmentResolver.Resolve(LogPrefix);
            if (resolved.Settings == null)
            {
                Debug.LogWarning($"{LogPrefix} ApiSettings is missing - proceeding without a saved configuration.");
                ProceedToEstimator();
                return;
            }

            _isConfirming = true;
            if (_confirmButton != null)
                _confirmButton.SetEnabled(false);

            _configurationApi = new ConfigurationApi(this, resolved.Settings, resolved.TrustAnyCertificate);
            var request = new CreateConfigurationRequest
            {
                vehicleModelId = selectedCar.id,
                exteriorColorOptionId = _selectedColorOptionId
            };
            _configurationApi.CreateConfiguration(request, accessToken, result =>
            {
                _isConfirming = false;
                if (_confirmButton != null)
                    _confirmButton.SetEnabled(true);

                if (result.Success)
                    gameManager.SelectedConfigurationId = result.Value.id;
                else
                    Debug.LogWarning($"{LogPrefix} Failed to create configuration: {result.Error.Detail}");

                ProceedToEstimator();
            });
        }

        // ReturnToEstimatorRequestedEvent is exactly what GameManager already
        // listens for to set ReturnToEstimator = true, which
        // DecideInitialScreen() checks to show the estimator card instead of
        // car selection - reusing that existing "return from AR" contract
        // rather than adding a new event for it.
        private void ProceedToEstimator()
        {
            SendVehicleInteractionTelemetry();
            EventBus.Publish(new ReturnToEstimatorRequestedEvent());
            HideUi();
            EventBus.Publish(new ExitArRequestedEvent());
        }

        // Best-effort, same philosophy as every other telemetry send in this
        // project. Sent even when _colourChangeCount is 0 (a real "customer
        // didn't touch customization" signal), as long as a telemetry
        // session and a selected car both exist. `this` is a safe coroutine
        // runner here - this component's GameObject is never destroyed on
        // AR exit (HideUi() just sets display:none).
        private void SendVehicleInteractionTelemetry()
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.TelemetrySessionId <= 0) return;

            var selectedCar = gameManager.SelectedCar;
            var accessToken = gameManager.AccessToken;
            if (selectedCar == null || selectedCar.id <= 0 || string.IsNullOrEmpty(accessToken)) return;

            var resolved = EnvironmentResolver.Resolve(LogPrefix);
            if (resolved.Settings == null) return;

            var telemetryApi = new TelemetryApi(this, resolved.Settings, resolved.TrustAnyCertificate);
            var request = new VehicleInteractionTelemetryRequest
            {
                customerSessionId = gameManager.TelemetrySessionId,
                clientVehicleInteractionId = _clientVehicleInteractionId,
                vehicleModelId = selectedCar.id,
                categoryName = PaintCategoryId,
                startedAt = _vehicleInteractionStartedAt.ToString("o"),
                endedAt = DateTime.UtcNow.ToString("o"),
                colourChangeCount = _colourChangeCount
            };
            telemetryApi.LogVehicleInteraction(request, accessToken, result =>
            {
                if (!result.Success)
                    Debug.LogWarning($"{LogPrefix} Vehicle interaction telemetry failed: {result.Error.Detail}");
            });
        }
    }
}
