using System;
using System.Collections.Generic;
using NAS.Core;
using NAS.Core.Events;
using NAS.Core.Motion;
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

        private const string WheelCategoryId = "wheel";
        private const string TrimsCategoryId = "trims";
        private const string InteriorCategoryId = "interior";

        private static readonly string[] CategoryIds = { WheelCategoryId, PaintCategoryId, TrimsCategoryId, InteriorCategoryId };
        private static readonly string[] CategoryButtonNames = { "category-wheel", "category-paint", "category-trims", "category-interior" };

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
        private bool _testDriveFailed;
        private Label _rotationValueLabel;
        private VisualElement _rotationFill;
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

        // Wheel and Interior switch the model's A/B meshes and Trims swap a glow shader, both on the placed car (CarVariantSwitcher, CarTrimController).
        private readonly Dictionary<string, Button> _categoryButtons = new Dictionary<string, Button>();
        private VisualElement _swatchRow;
        private Label _swatchCaption;
        private Button _selectedSwatch;
        private CarVariantSwitcher _variantSwitcher;
        private CarComponents _carComponents;
        private int _selectedTrimStyle;
        private string _activeCategoryId;

        private IConfigurationApi _configurationApi;
        private bool _isConfirming;

        // VehicleInteraction telemetry - paint is currently the only real
        // customization surface (see the Dictionary comment above), so this
        // is the sole source of colour_changes telemetry today. Reset per AR
        // visit in ShowForCurrentCar(), sent once when the customer leaves AR
        // (back or confirm) in SendVehicleInteractionTelemetry().
        private int _colourChangeCount;
        private int _trimChangeCount;
        private int _zoomInCount;
        private int _zoomOutCount;
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
            _swatchCaption = root.Q<Label>("swatch-caption");
            _rotationSlider = root.Q<Slider>("rotation-slider");
            _rotationSliderRow = root.Q<VisualElement>("rotation-slider-row");
            _rotationValueLabel = root.Q<Label>("rotation-value-label");
            _rotationFill = AddSliderFill(_rotationSlider);
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
            EventBus.Subscribe<SessionEndedEvent>(OnSessionEnded);
            EventBus.Subscribe<CarScaleChangedEvent>(OnCarScaleChanged);
            EventBus.Subscribe<ZoomCountsUpdatedEvent>(OnZoomCountsUpdated);
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
            Selection?.Clear();
            _selectedTrimStyle = 0;
            _variantSwitcher = null;
            _carComponents = null;
            if (_swatchRow != null)
                _swatchRow.Clear();
            SetElementVisible(_swatchCaption, false);

            // SetValueWithoutNotify - a plain `.value = 0` would fire
            // RotationSliderChangedEvent through the normal callback path,
            // which is fine in itself (CarManipulationController would just
            // reset the not-yet-placed car's rotation to 0, a no-op), but
            // relying on that side effect to reset the label/slider in sync
            // is more fragile than just doing it directly here.
            _rotationSlider?.SetValueWithoutNotify(0f);
            RefreshRotationVisuals(0f);
            _verticalOffsetSlider?.SetValueWithoutNotify(0f);
            if (_scaleHintLabel != null)
                _scaleHintLabel.text = "Pinch to scale [1:1]";

            // Hidden again on every fresh AR entry until CarPlacedEvent fires
            // for whatever gets placed this visit - showing controls for a
            // car that isn't in the scene yet is confusing.
            SetElementVisible(_rotationSliderRow, false);
            SetElementVisible(_verticalOffsetSliderColumn, false);
            SetElementVisible(_settingsButtonRow, false);
            _testDriveFailed = false; // a failure belonged to the previous visit/car
            RefreshTestDriveButton();

            _colourChangeCount = 0;
            _trimChangeCount = 0;
            _zoomInCount = 0;
            _zoomOutCount = 0;
            _clientVehicleInteractionId = Guid.NewGuid().ToString();
            _vehicleInteractionStartedAt = DateTime.UtcNow;
        }

        private void OnEnterAr(EnterArRequestedEvent evt) => ShowForCurrentCar();

        // Signed out while in AR (an expired login): just hide. No telemetry - the session is gone and it would be rejected.
        private void OnSessionEnded(SessionEndedEvent evt) => HideUi();

        private void OnRotationSliderGrabbed(PointerDownEvent evt) =>
            EventBus.Publish(new RotationSliderGrabbedEvent());

        private void OnRotationSliderValueChanged(ChangeEvent<float> evt)
        {
            RefreshRotationVisuals(evt.newValue);
            EventBus.Publish(new RotationSliderChangedEvent(evt.newValue));
        }

        // The Figma slider fills with the accent colour up to the thumb and shows the angle beside it. UI Toolkit's
        // Slider has no fill of its own, so one is added inside the track (under the thumb) and sized from the value.
        private static VisualElement AddSliderFill(Slider slider)
        {
            var tracker = slider?.Q<VisualElement>(className: "unity-base-slider__tracker");
            if (tracker == null) return null;
            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("slider-fill");
            tracker.Insert(0, fill);
            return fill;
        }

        private void RefreshRotationVisuals(float value)
        {
            if (_rotationValueLabel != null)
                _rotationValueLabel.text = $"{Mathf.RoundToInt(value)}\u00B0";
            if (_rotationFill != null && _rotationSlider != null)
            {
                var range = _rotationSlider.highValue - _rotationSlider.lowValue;
                var percent = range > 0f ? (value - _rotationSlider.lowValue) / range * 100f : 0f;
                _rotationFill.style.width = Length.Percent(Mathf.Clamp(percent, 0f, 100f));
            }
        }

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
            _variantSwitcher = evt.Instance != null ? evt.Instance.GetComponent<CarVariantSwitcher>() : null;
            _carComponents = evt.Instance != null ? evt.Instance.GetComponent<CarComponents>() : null;
            SetElementVisible(_rotationSliderRow, true);
            SetElementVisible(_verticalOffsetSliderColumn, true);
            SetElementVisible(_settingsButtonRow, true);
        }

        private void RefreshTestDriveButton()
        {
            if (_testDriveButton == null) return;
            var selectedCar = GameManager.Instance != null ? GameManager.Instance.SelectedCar : null;
            var done = selectedCar != null && selectedCar.id > 0 && selectedCar.id == _testDriveRequestedForCarId;
            // Four states, as in the Figma design: default, sending, confirmed for this car, failed (stays tappable).
            var failed = !done && !_isRequestingTestDrive && _testDriveFailed;
            _testDriveButton.text = done ? "Test drive requested \u2713"
                : _isRequestingTestDrive ? "Sending..."
                : failed ? "Try again"
                : "Request Test Drive";
            _testDriveButton.EnableInClassList("test-drive-button--done", done);
            _testDriveButton.EnableInClassList("test-drive-button--sending", _isRequestingTestDrive);
            _testDriveButton.EnableInClassList("test-drive-button--failed", failed);
            _testDriveButton.SetEnabled(!done && !_isRequestingTestDrive);

            // The result lands with a little motion: a pop when confirmed, a shake when it failed.
            if (done && !_testDriveWasDone) UiMotion.Pop(_testDriveButton, 0.94f, 0.4f);
            if (failed && !_testDriveWasFailed) UiMotion.Shake(_testDriveButton, 6f, 0.3f);
            _testDriveWasDone = done;
            _testDriveWasFailed = failed;
        }

        private bool _testDriveWasDone;
        private bool _testDriveWasFailed;

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
            _testDriveFailed = false;
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
                {
                    _testDriveFailed = true;
                    Debug.LogWarning($"{LogPrefix} Test drive request failed: {result.Error.Detail}");
                }
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

        private void OnZoomCountsUpdated(ZoomCountsUpdatedEvent evt)
        {
            _zoomInCount = evt.ZoomIn;
            _zoomOutCount = evt.ZoomOut;
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
            EventBus.Unsubscribe<SessionEndedEvent>(OnSessionEnded);
            EventBus.Unsubscribe<CarScaleChangedEvent>(OnCarScaleChanged);
            EventBus.Unsubscribe<ZoomCountsUpdatedEvent>(OnZoomCountsUpdated);
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
            var wasOpen = _customizeSheet != null && _customizeSheet.style.display == DisplayStyle.Flex;
            if (open)
            {
                SetElementVisible(_sheetBackdrop, true);
                SetElementVisible(_customizeSheet, true);
                UiMotion.FadeIn(_sheetBackdrop, 0.22f);
                SlideSheet(0.38f, Ease.OutCubic, from: 100f, to: 0f, onComplete: null);
            }
            else if (wasOpen)
            {
                // Slide away first, then take it out of the layout so it stops catching taps.
                UiMotion.FadeOut(_sheetBackdrop, 0.2f);
                SlideSheet(0.26f, Ease.InCubic, from: 0f, to: 100f, onComplete: () =>
                {
                    SetElementVisible(_sheetBackdrop, false);
                    SetElementVisible(_customizeSheet, false);
                });
            }
            else
            {
                // Already closed (e.g. the per-visit reset): nothing to animate.
                SetElementVisible(_sheetBackdrop, false);
                SetElementVisible(_customizeSheet, false);
            }
            EventBus.Publish(new CustomizeSheetToggledEvent(open));
        }

        // The sheet moves as a percentage of its own height, so it slides the whole way whatever the screen size.
        private void SlideSheet(float duration, Ease ease, float from, float to, System.Action onComplete)
        {
            if (_customizeSheet == null) { onComplete?.Invoke(); return; }
            _customizeSheet.style.translate = new Translate(0, Length.Percent(from));
            UiMotion.Run(_customizeSheet, duration, ease,
                v => _customizeSheet.style.translate = new Translate(0, Length.Percent(Mathf.LerpUnclamped(from, to, v))),
                0f, "sheet", onComplete);
        }

        private void OnCategoryClicked(string categoryId)
        {
            _activeCategoryId = categoryId;

            foreach (var pair in _categoryButtons)
                pair.Value.EnableInClassList("category-button--active", pair.Key == categoryId);

            switch (categoryId)
            {
                case PaintCategoryId: ShowPaintSwatches(); break;
                case TrimsCategoryId: ShowTrimStyles(); break;
                default: ShowVariants(categoryId); break;
            }
        }

        private void ShowCaption(string text)
        {
            if (_swatchCaption == null) return;
            _swatchCaption.text = text;
            SetElementVisible(_swatchCaption, true);
        }

        private void ShowUnavailable(string what)
        {
            SetElementVisible(_swatchRow, false);
            ShowCaption($"{what} can't be changed on this car.");
        }

        private void ShowPaintSwatches()
        {
            var selectedCar = GameManager.Instance != null ? GameManager.Instance.SelectedCar : null;
            var colors = selectedCar != null ? selectedCar.exteriorColors : null;

            if (_swatchRow == null)
                return;

            _swatchRow.Clear();
            _selectedSwatch = null;

            if (colors == null || colors.Count == 0)
            {
                SetElementVisible(_swatchRow, false);
                ShowCaption("No paint colours are set up for this car.");
                return;
            }

            SetElementVisible(_swatchRow, true);
            SetElementVisible(_swatchCaption, false);

            foreach (var option in colors)
            {
                var swatch = new Button { name = $"swatch-{option.id}" };
                swatch.AddToClassList("color-swatch");

                if (ColorUtility.TryParseHtmlString(option.hexCode, out var color))
                    swatch.style.backgroundColor = new StyleColor(color);

                swatch.clicked += () => OnSwatchClicked(swatch, option);
                AddChoice(swatch, option.priceAdjustment);
            }
        }

        private void ShowVariants(string categoryId)
        {
            if (_swatchRow == null) return;
            _swatchRow.Clear();
            _selectedSwatch = null;

            var group = categoryId == WheelCategoryId ? ModelVariantRules.Wheels : ModelVariantRules.Interior;
            var title = categoryId == WheelCategoryId ? "Wheels" : "Interior";
            var letters = _variantSwitcher != null ? _variantSwitcher.Variants(group) : null;
            if (letters == null || letters.Count < 2)
            {
                ShowUnavailable(title);
                return;
            }

            SetElementVisible(_swatchRow, true);
            var chosen = _variantSwitcher.Chosen(group);
            var selectedCar = GameManager.Instance != null ? GameManager.Instance.SelectedCar : null;
            var options = OptionsOf(selectedCar == null ? null : categoryId == WheelCategoryId ? selectedCar.wheels : selectedCar.interiors);
            foreach (var letter in letters)
            {
                var variant = letter;
                var chip = new Button { name = $"variant-{group}-{variant}", text = variant };
                chip.AddToClassList("color-swatch");
                chip.AddToClassList("color-swatch--variant");

                var preview = _variantSwitcher.Preview(group, variant);
                if (preview.HasValue)
                    chip.style.backgroundColor = new StyleColor(preview.Value);

                var option = OptionMatch.ForVariant(options, variant);
                chip.clicked += () => OnVariantClicked(chip, group, title, variant, option);
                if (variant == chosen) SelectChip(chip);
                AddChoice(chip, option?.Price ?? 0f);
            }
            ShowCaption($"{title} style {chosen}");
        }

        private void OnVariantClicked(Button chip, string group, string title, string variant, OptionRef? option)
        {
            if (group == ModelVariantRules.Wheels) Selection?.SetWheel(option?.Id ?? 0, option?.Price ?? 0f);
            else Selection?.SetInterior(option?.Id ?? 0, option?.Price ?? 0f);
            SelectChip(chip);
            ShowCaption($"{title} style {variant}");
            EventBus.Publish(new ModelVariantSelectedEvent(group, variant));
        }

        private void ShowTrimStyles()
        {
            if (_swatchRow == null) return;
            _swatchRow.Clear();
            _selectedSwatch = null;

            if (_carComponents == null || _carComponents.Trim.Count == 0)
            {
                ShowUnavailable("Trims");
                return;
            }

            SetElementVisible(_swatchRow, true);
            var selectedCar = GameManager.Instance != null ? GameManager.Instance.SelectedCar : null;
            var levels = OptionsOf(selectedCar?.trimLevels);
            for (var i = 0; i < TrimStyles.All.Count; i++)
            {
                var index = i;
                var style = TrimStyles.All[i];
                var level = OptionMatch.ForName(levels, style.Name);
                var chip = new Button { name = $"trim-style-{i}" };
                chip.AddToClassList("color-swatch");

                if (style.IsOriginal)
                {
                    chip.AddToClassList("color-swatch--original");
                    chip.text = "\u21BA";
                }
                else
                {
                    if (ColorUtility.TryParseHtmlString(style.Hex, out var color))
                        chip.style.backgroundColor = new StyleColor(color);
                    chip.AddToClassList("color-swatch--variant");
                    chip.text = style.Mode == TrimMode.Flow ? "~" : style.Mode == TrimMode.Pulse ? "\u25CF" : string.Empty;
                }

                chip.clicked += () => OnTrimStyleClicked(chip, index, level);
                if (i == _selectedTrimStyle) SelectChip(chip);
                AddChoice(chip, level?.Price ?? 0f);
            }
            ShowCaption(TrimStyles.All[_selectedTrimStyle].Label);
        }

        private void OnTrimStyleClicked(Button chip, int index, OptionRef? level)
        {
            var style = TrimStyles.All[index];
            var previousStyle = _selectedTrimStyle;
            _selectedTrimStyle = index;
            Selection?.SetTrim(level?.Id ?? 0, level?.Price ?? 0f);
            if (index != previousStyle) _trimChangeCount++;
            SelectChip(chip);
            ShowCaption(style.Label);
            EventBus.Publish(new TrimStyleSelectedEvent(style.Hex, (int)style.Mode, style.Intensity));
        }

        private CustomizationSelection Selection => GameManager.Instance != null ? GameManager.Instance.Customization : null;

        private static List<OptionRef> OptionsOf(List<CarColorOption> options)
        {
            var refs = new List<OptionRef>();
            if (options != null)
                foreach (var option in options) refs.Add(new OptionRef(option.id, option.name, option.priceAdjustment));
            return refs;
        }

        private void AddChoice(Button chip, float price)
        {
            var item = new VisualElement();
            item.AddToClassList("swatch-item");
            item.Add(chip);
            var tag = new Label(OptionMatch.PriceTag(price));
            tag.AddToClassList("swatch-price");
            item.Add(tag);
            _swatchRow.Add(item);
        }

        private void SelectChip(Button chip)
        {
            _selectedSwatch?.RemoveFromClassList("color-swatch--selected");
            chip.AddToClassList("color-swatch--selected");
            _selectedSwatch = chip;
        }

        private void OnSwatchClicked(Button swatch, CarColorOption option)
        {
            if (_selectedSwatch != null)
                _selectedSwatch.RemoveFromClassList("color-swatch--selected");

            swatch.AddToClassList("color-swatch--selected");
            _selectedSwatch = swatch;
            Selection?.SetColour(option.id, option.priceAdjustment);
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

        // Saves the whole Customize choice before the Estimator, best-effort.
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
                exteriorColorOptionId = Selection?.ExteriorColorOptionId ?? 0,
                wheelOptionId = Selection?.WheelOptionId ?? 0,
                interiorOptionId = Selection?.InteriorOptionId ?? 0,
                trimLevelId = Selection?.TrimLevelId ?? 0
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
                colourChangeCount = _colourChangeCount,
                trimChangeCount = _trimChangeCount,
                zoomInCount = _zoomInCount,
                zoomOutCount = _zoomOutCount
            };
            telemetryApi.LogVehicleInteraction(request, accessToken, result =>
            {
                if (!result.Success)
                    Debug.LogWarning($"{LogPrefix} Vehicle interaction telemetry failed: {result.Error.Detail}");
            });
        }
    }
}
