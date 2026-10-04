using System;
using UnityEngine;
using UnityEngine.UIElements;
using NAS.Core.Models;
using NAS.Core.Interfaces;
using NAS.Core.Services;
using NAS.Core.Events;
using NAS.Core.Networking;
using NAS.Core;

namespace NAS.UI.Controllers
{
    public class EstimatorCardController : MonoBehaviour
    {
        [SerializeField] private Label _vehicleInfoLabel;
        [SerializeField] private Label _retailPriceLabel;
        [SerializeField] private TextField _depositField;
        [SerializeField] private TextField _tradeInField;
        [SerializeField] private TextField _loanTermField;
        [SerializeField] private Label _financedAmountLabel;
        [SerializeField] private Slider _interestSlider;
        [SerializeField] private Label _interestValueLabel;
        [SerializeField] private Slider _balloonSlider;
        [SerializeField] private Label _balloonPercentLabel;
        [SerializeField] private Label _balloonDollarLabel;
        [SerializeField] private Label _monthlyPaymentLabel;
        [SerializeField] private Label _balloonInfoLabel;
        [SerializeField] private Label _loanInterestInfoLabel;
        [SerializeField] private Button _sendButton;
        [SerializeField] private VisualElement _balloonContainer;
        [SerializeField] private Label _sendErrorLabel;

        private const string LogPrefix = "[NAS Estimator]";

        // Value-based fill bars for the two sliders - UI Toolkit's Slider
        // has no built-in "filled portion" and USS here has no gradient
        // background support, so this is a plain VisualElement inserted
        // into the slider's internal drag-container and resized on every
        // value change (see CreateSliderFill/UpdateSliderFill below).
        private VisualElement _interestSliderFill;
        private VisualElement _balloonSliderFill;

        private VehicleInfo _vehicle;
        private ILoanCalculator _loanCalculator;
        private IEstimatorApi _estimatorApi;
        private bool _isSubmitting;

        private float _deposit;
        private float _tradeIn;
        private int _loanTerm;
        private float _interestRate;
        private float _balloonPercent;

        // AffordabilitySession telemetry - snapshotted once in OnEnable(),
        // sent once in OnDisable() with whatever the fields ended up at.
        // Deliberately sent on every exit (not only on "Send to Dealer") -
        // a customer who fiddled with the calculator and left without
        // submitting is exactly the warm/unconverted signal this exists to
        // capture, not something to discard.
        private float _initialDeposit;
        private float _initialTradeIn;
        private int _initialTermMonths;
        private float _initialInterestRate;
        private int _depositChangeCount;
        private int _tradeInChangeCount;
        private int _termChangeCount;
        private int _interestRateChangeCount;
        private int _calculationCount;
        private string _clientAffordabilitySessionId;
        private DateTime _affordabilitySessionStartedAt;

        private void OnEnable()
        {
            _loanCalculator = new LoanCalculator();
            
            // Use selected car from GameManager if available, otherwise fallback to default
            if (GameManager.Instance != null && GameManager.Instance.SelectedCar != null)
            {
                _vehicle = GameManager.Instance.SelectedCar;
            }
            else
            {
                _vehicle = new VehicleInfo
                {
                    modelName = "Tesla Model S · Performance Trim",
                    retailPrice = 75000f
                };
            }
            
            _deposit = 0f;
            _tradeIn = 0f;
            _loanTerm = 48;
            _interestRate = 4.5f;
            _balloonPercent = 0f;

            _initialDeposit = _deposit;
            _initialTradeIn = _tradeIn;
            _initialTermMonths = _loanTerm;
            _initialInterestRate = _interestRate;
            _depositChangeCount = 0;
            _tradeInChangeCount = 0;
            _termChangeCount = 0;
            _interestRateChangeCount = 0;
            _calculationCount = 0;
            _clientAffordabilitySessionId = Guid.NewGuid().ToString();
            _affordabilitySessionStartedAt = DateTime.UtcNow;

            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;

            var root = uiDocument.rootVisualElement;
            
            if (_vehicleInfoLabel == null) _vehicleInfoLabel = root.Q<Label>("vehicle-info");
            if (_retailPriceLabel == null) _retailPriceLabel = root.Q<Label>("retail-price");
            if (_depositField == null) _depositField = root.Q<TextField>("deposit-field");
            if (_tradeInField == null) _tradeInField = root.Q<TextField>("tradein-field");
            if (_loanTermField == null) _loanTermField = root.Q<TextField>("loan-term-field");
            if (_financedAmountLabel == null) _financedAmountLabel = root.Q<Label>("financed-amount");
            if (_interestSlider == null) _interestSlider = root.Q<Slider>("interest-slider");
            if (_interestValueLabel == null) _interestValueLabel = root.Q<Label>("interest-rate-value");
            if (_balloonSlider == null) _balloonSlider = root.Q<Slider>("balloon-slider");
            if (_balloonPercentLabel == null) _balloonPercentLabel = root.Q<Label>("balloon-percent-value");
            if (_balloonDollarLabel == null) _balloonDollarLabel = root.Q<Label>("balloon-dollar-value");
            if (_monthlyPaymentLabel == null) _monthlyPaymentLabel = root.Q<Label>("monthly-payment");
            if (_balloonInfoLabel == null) _balloonInfoLabel = root.Q<Label>("balloon-info");
            if (_loanInterestInfoLabel == null) _loanInterestInfoLabel = root.Q<Label>("loan_interest_info");
            if (_sendButton == null) _sendButton = root.Q<Button>("send-button");
            if (_balloonContainer == null) _balloonContainer = root.Q<VisualElement>("balloon-info-container");
            if (_sendErrorLabel == null) _sendErrorLabel = root.Q<Label>("estimator-error-label");

            // Display the selected car's name/price - previously the header
            // label was a hardcoded "Tesla Model S · Performance Trim" string
            // in the UXML even when a real selected car came through
            // GameManager; retail price already used the real value, this
            // brings the vehicle-info label in line with it.
            if (_vehicleInfoLabel != null) _vehicleInfoLabel.text = _vehicle.modelName;
            _retailPriceLabel.text = $"R{_vehicle.retailPrice:N0}";
            
            _depositField.value = $"{_deposit}";
            _tradeInField.value = $"{_tradeIn}";
            _loanTermField.value = $"{_loanTerm}";
            _interestSlider.value = _interestRate;
            _balloonSlider.value = _balloonPercent;

            _interestSliderFill = CreateSliderFill(_interestSlider);
            _balloonSliderFill = CreateSliderFill(_balloonSlider);
            UpdateSliderFill(_interestSlider, _interestSliderFill);
            UpdateSliderFill(_balloonSlider, _balloonSliderFill);

            _depositField.RegisterValueChangedCallback(evt =>
            {
                var sanitized = SanitizeFloatInput(evt.newValue);
                if (sanitized != evt.newValue)
                    _depositField.SetValueWithoutNotify(sanitized);
                if (float.TryParse(sanitized, out var value))
                {
                    _deposit = ValidateDeposit(value);
                    _depositChangeCount++;
                    _calculationCount++;
                    UpdateAll();
                }
            });
            _tradeInField.RegisterValueChangedCallback(evt =>
            {
                var sanitized = SanitizeFloatInput(evt.newValue);
                if (sanitized != evt.newValue)
                    _tradeInField.SetValueWithoutNotify(sanitized);
                if (float.TryParse(sanitized, out var value))
                {
                    _tradeIn = ValidateTradeIn(value);
                    _tradeInChangeCount++;
                    _calculationCount++;
                    UpdateAll();
                }
            });
            _loanTermField.RegisterValueChangedCallback(evt =>
            {
                var sanitized = SanitizeIntInput(evt.newValue);
                if (sanitized != evt.newValue)
                    _loanTermField.SetValueWithoutNotify(sanitized);
                if (int.TryParse(sanitized, out var value))
                {
                    _loanTerm = ValidateLoanTerm(value);
                    _termChangeCount++;
                    _calculationCount++;
                    UpdateAll();
                }
            });
            _interestSlider.RegisterValueChangedCallback(evt =>
            {
                _interestRate = ValidateInterestRate(evt.newValue);
                if (Mathf.Abs(_interestSlider.value - _interestRate) > 0.01f)
                    _interestSlider.SetValueWithoutNotify(_interestRate);
                UpdateSliderFill(_interestSlider, _interestSliderFill);
                _interestRateChangeCount++;
                _calculationCount++;
                UpdateAll();
            });
            _balloonSlider.RegisterValueChangedCallback(evt =>
            {
                _balloonPercent = ValidateBalloonPercent(evt.newValue);
                if (Mathf.Abs(_balloonSlider.value - _balloonPercent) > 0.01f)
                    _balloonSlider.SetValueWithoutNotify(_balloonPercent);
                UpdateSliderFill(_balloonSlider, _balloonSliderFill);
                _calculationCount++; // no dedicated backend counter for balloon - still a real recalculation
                UpdateAll();
            });
            
            _sendButton.clicked += OnSendToDealer;
            UpdateAll();
        }

        // Inserted as a CHILD of the tracker itself, not a sibling in the
        // drag-container - confirmed live that drag-container is much
        // taller than the tracker (24px vs ~6px, sized to fit the 18px
        // dragger knob plus margin), so a sibling anchored top:0/bottom:0
        // against drag-container rendered as a tall, blocky rectangle
        // instead of a slim bar matching the actual track. As tracker's
        // child, top:0/bottom:0 correctly resolves against tracker's own
        // slim height instead.
        private VisualElement CreateSliderFill(Slider slider)
        {
            var tracker = slider.Q(className: "unity-base-slider__tracker");
            if (tracker == null) return null;

            var fill = new VisualElement { pickingMode = PickingMode.Ignore };
            fill.AddToClassList("slider-fill");
            tracker.Add(fill);
            return fill;
        }

        private void UpdateSliderFill(Slider slider, VisualElement fill)
        {
            if (fill == null) return;
            float range = slider.highValue - slider.lowValue;
            float percent = range > 0f ? (slider.value - slider.lowValue) / range * 100f : 0f;
            fill.style.width = new Length(percent, LengthUnit.Percent);
        }

        private float ValidateDeposit(float value) => Mathf.Clamp(value, 0, _vehicle.retailPrice);
        private float ValidateTradeIn(float value) => Mathf.Clamp(value, 0, _vehicle.retailPrice);
        private int ValidateLoanTerm(int value) => Mathf.Clamp(value, 1, 120);
        private float ValidateInterestRate(float value) => Mathf.Clamp(value, 0f, 30f);
        private float ValidateBalloonPercent(float value) => Mathf.Clamp(value, 0f, 50f);

        private string SanitizeFloatInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;
            bool hasDecimal = false;
            var result = new System.Text.StringBuilder();
            foreach (var c in input)
            {
                if (char.IsDigit(c))
                    result.Append(c);
                else if ((c == '.' || c == ',') && !hasDecimal)
                {
                    result.Append('.');
                    hasDecimal = true;
                }
            }
            return result.ToString();
        }

        private string SanitizeIntInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;
            var result = new System.Text.StringBuilder();
            foreach (var c in input)
            {
                if (char.IsDigit(c))
                    result.Append(c);
            }
            return result.ToString();
        }

        private void UpdateAll()
        {
            float financed = _vehicle.retailPrice - _deposit - _tradeIn;
            if (financed < 0) financed = 0;
            _financedAmountLabel.text = $"R{financed:N0}";
            
            float balloonAmount = financed * (_balloonPercent / 100f);
            _balloonPercentLabel.text = $"{_balloonPercent:F0}%";
            _balloonDollarLabel.text = $"({balloonAmount:C0})";
            _interestValueLabel.text = $"{_interestRate:F1}%";
            _loanInterestInfoLabel.text = $"* Estimated payment based on {_interestRate:F1}% interest. Actual rates may vary.";
            
            float monthly = _loanCalculator.CalculateMonthlyPayment(financed, balloonAmount, _loanTerm, _interestRate);
            _monthlyPaymentLabel.text = $"{monthly:C0}";
            
            if (_balloonPercent > 0)
            {
                _balloonInfoLabel.text = $"+ {balloonAmount:C0} balloon due at end";
                _balloonContainer.style.display = DisplayStyle.Flex;
            }
            else
            {
                _balloonContainer.style.display = DisplayStyle.None;
            }
        }

        private void OnSendToDealer()
        {
            if (_isSubmitting) return; // Button is disabled while submitting, but guard anyway
            SetSendError(null);

            float financed = _vehicle.retailPrice - _deposit - _tradeIn;
            if (financed < 0) financed = 0;
            float balloonAmount = financed * (_balloonPercent / 100f);
            float monthly = _loanCalculator.CalculateMonthlyPayment(financed, balloonAmount, _loanTerm, _interestRate);

            if (_vehicle.id <= 0)
            {
                SetSendError("No vehicle selected - go back and pick a car first.");
                return;
            }

            var resolved = EnvironmentResolver.Resolve(LogPrefix);
            if (resolved.Settings == null)
            {
                Debug.LogError($"{LogPrefix} ApiSettings is missing on EstimatorCardController.");
                SetSendError("Something went wrong. Please try again later.");
                return;
            }

            var accessToken = GameManager.Instance != null ? GameManager.Instance.AccessToken : null;
            if (string.IsNullOrEmpty(accessToken))
            {
                SetSendError("Please sign in to continue.");
                return;
            }

            _estimatorApi = new EstimatorApi(this, resolved.Settings, resolved.TrustAnyCertificate);
            var request = new SubmitEstimateRequest
            {
                vehicleModelId = _vehicle.id,
                depositAmount = _deposit,
                tradeInValue = _tradeIn,
                termMonths = _loanTerm,
                interestRate = _interestRate,
                estimatedMonthly = monthly,
                balloonAmount = balloonAmount,
                savedConfigurationId = GameManager.Instance != null ? GameManager.Instance.SelectedConfigurationId : 0,
                customerSessionId = GameManager.Instance != null ? GameManager.Instance.TelemetrySessionId : 0
            };

            SetSubmitting(true);
            EventBus.Publish(new EstimateSubmissionStartedEvent());
            _estimatorApi.SubmitEstimate(request, accessToken, result =>
            {
                SetSubmitting(false);
                if (result.Success)
                {
                    EventBus.Publish(new EstimateSubmittedEvent(_vehicle, financed, monthly));
                }
                else
                {
                    Debug.LogError($"{LogPrefix} Submit failed: {result.Error.Detail}");
                    SetSendError(result.Error.UserMessage);
                    EventBus.Publish(new EstimateSubmissionFailedEvent());
                }
            });
        }

        private void SetSubmitting(bool submitting)
        {
            _isSubmitting = submitting;
            if (_sendButton == null) return;
            _sendButton.SetEnabled(!submitting);
            _sendButton.text = submitting ? "Sending..." : "Send to Dealer";
        }

        private void SetSendError(string message)
        {
            if (_sendErrorLabel == null) return;
            _sendErrorLabel.text = message ?? string.Empty;
            _sendErrorLabel.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnDisable()
        {
            if (_sendButton != null) _sendButton.clicked -= OnSendToDealer;
            SendAffordabilityTelemetry();
        }

        // Best-effort, same philosophy as every other telemetry send in this
        // project - must never throw or block teardown. Uses
        // GameManager.Instance as the coroutine runner, NOT `this`: by the
        // time OnDisable()'s body runs, this component is already disabled
        // (it's being destroyed via ParentPageController.RemoveCardControllers()),
        // and Unity refuses to start a new coroutine on an already-disabled
        // MonoBehaviour. GameManager is the DontDestroyOnLoad singleton that
        // stays enabled through this teardown.
        private void SendAffordabilityTelemetry()
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.TelemetrySessionId <= 0) return;

            var accessToken = gameManager.AccessToken;
            if (string.IsNullOrEmpty(accessToken) || _vehicle == null || _vehicle.id <= 0) return;

            var resolved = EnvironmentResolver.Resolve(LogPrefix);
            if (resolved.Settings == null) return;

            float financed = _vehicle.retailPrice - _deposit - _tradeIn;
            if (financed < 0) financed = 0;
            float balloonAmount = financed * (_balloonPercent / 100f);
            float finalMonthly = _loanCalculator.CalculateMonthlyPayment(financed, balloonAmount, _loanTerm, _interestRate);
            float initialFinanced = Mathf.Max(0f, _vehicle.retailPrice - _initialDeposit - _initialTradeIn);
            float initialMonthly = _loanCalculator.CalculateMonthlyPayment(initialFinanced, 0f, _initialTermMonths, _initialInterestRate);

            var telemetryApi = new TelemetryApi(gameManager, resolved.Settings, resolved.TrustAnyCertificate);
            var request = new AffordabilitySessionTelemetryRequest
            {
                customerSessionId = gameManager.TelemetrySessionId,
                clientAffordabilitySessionId = _clientAffordabilitySessionId,
                vehicleModelId = _vehicle.id,
                startedAt = _affordabilitySessionStartedAt.ToString("o"),
                endedAt = DateTime.UtcNow.ToString("o"),
                initialDeposit = _initialDeposit,
                finalDeposit = _deposit,
                initialTradeIn = _initialTradeIn,
                finalTradeIn = _tradeIn,
                initialTermMonths = _initialTermMonths,
                finalTermMonths = _loanTerm,
                initialInterestRate = _initialInterestRate,
                finalInterestRate = _interestRate,
                initialMonthlyPayment = initialMonthly,
                finalMonthlyPayment = finalMonthly,
                calculationCount = _calculationCount,
                depositChangeCount = _depositChangeCount,
                tradeInChangeCount = _tradeInChangeCount,
                termChangeCount = _termChangeCount,
                interestRateChangeCount = _interestRateChangeCount
            };
            telemetryApi.LogAffordabilitySession(request, accessToken, result =>
            {
                if (!result.Success)
                    Debug.LogWarning($"{LogPrefix} Affordability telemetry failed: {result.Error.Detail}");
            });
        }
    }
}