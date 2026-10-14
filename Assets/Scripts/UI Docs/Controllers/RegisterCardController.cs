using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using NAS.Core.Events;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// Purely a view. Publishes what the user did — AuthController decides if
    /// it's valid, ParentPageController decides where to go next.
    /// </summary>
    public class RegisterCardController : MonoBehaviour
    {

        [SerializeField] private TextField _firstNameField;
        [SerializeField] private TextField _lastNameField;
        [SerializeField] private TextField _cellNumberField;
        [SerializeField] private TextField _emailField;
        [SerializeField] private TextField _passwordField;
        [SerializeField] private TextField _confirmPasswordField;
        [SerializeField] private Button _registerButton;
        [SerializeField] private Label _registerButtonLabel;
        [SerializeField] private VisualElement _registerButtonSpinner;
        [SerializeField] private Label _richTextLabel;
        [SerializeField] private Label _errorLabel; // general/backend-level error, "register-error-label"

        [SerializeField] private Label _firstNameErrorLabel;
        [SerializeField] private Label _lastNameErrorLabel;
        [SerializeField] private Label _cellNumberErrorLabel;
        [SerializeField] private Label _emailErrorLabel;
        [SerializeField] private Label _passwordErrorLabel;
        [SerializeField] private Label _confirmPasswordErrorLabel;

        [SerializeField] private Button _passwordToggleButton;
        [SerializeField] private Button _confirmPasswordToggleButton;

        private bool _isSubmitting;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;

            var root = uiDocument.rootVisualElement;

            _firstNameField = root.Q<TextField>("first-name-field");
            _lastNameField = root.Q<TextField>("last-name-field");
            _cellNumberField = root.Q<TextField>("cell-number-field");
            _emailField = root.Q<TextField>("email-field");
            _passwordField = root.Q<TextField>("password-field");
            _confirmPasswordField = root.Q<TextField>("confirm-password-field");
            _registerButton = root.Q<Button>("register-button");
            _registerButtonLabel = root.Q<Label>("register-button-label");
            _registerButtonSpinner = root.Q<VisualElement>("register-button-spinner");
            _richTextLabel = root.Q<Label>("login-link-label");
            _errorLabel = root.Q<Label>("register-error-label");

            _firstNameErrorLabel = root.Q<Label>("first-name-error-label");
            _lastNameErrorLabel = root.Q<Label>("last-name-error-label");
            _cellNumberErrorLabel = root.Q<Label>("cell-number-error-label");
            _emailErrorLabel = root.Q<Label>("email-error-label");
            _passwordErrorLabel = root.Q<Label>("password-error-label");
            _confirmPasswordErrorLabel = root.Q<Label>("confirm-password-error-label");

            _passwordToggleButton = root.Q<Button>("password-toggle-button");
            _confirmPasswordToggleButton = root.Q<Button>("confirm-password-toggle-button");

            if (_registerButton != null)
                _registerButton.clicked += OnRegisterClicked;

            if (_richTextLabel != null)
                _richTextLabel.RegisterCallback<PointerUpLinkTagEvent>(OnLinkClicked);

            if (_passwordToggleButton != null)
            {
                _passwordToggleButton.clicked += TogglePasswordVisibility;
                _passwordToggleButton.text = string.Empty;
                UpdateToggleButtonIcon(_passwordToggleButton, true);
                if (_passwordField != null) _passwordField.isPasswordField = true;
            }

            if (_confirmPasswordToggleButton != null)
            {
                _confirmPasswordToggleButton.clicked += ToggleConfirmPasswordVisibility;
                _confirmPasswordToggleButton.text = string.Empty;
                UpdateToggleButtonIcon(_confirmPasswordToggleButton, true);
                if (_confirmPasswordField != null) _confirmPasswordField.isPasswordField = true;
            }

            EventBus.Subscribe<AuthFailedEvent>(OnAuthFailed);
        }

        private void OnRegisterClicked()
        {
            if (_isSubmitting) return; // Button is disabled while submitting, but guard anyway
            ClearAllErrors();
            SetSubmitting(true);
            EventBus.Publish(new RegisterRequestedEvent(
                _firstNameField?.value,
                _lastNameField?.value,
                _cellNumberField?.value,
                _emailField?.value,
                _passwordField?.value,
                _confirmPasswordField?.value));
        }

        // No explicit "success" handler needed here - on success this whole
        // card gets destroyed by ParentPageController's navigation (see
        // SessionAuthenticatedEvent in GameEvents.cs), so there's nothing to
        // revert the button back to. Only the failure path needs to turn the
        // spinner back off, since the form stays up for the user to retry.
        private void SetSubmitting(bool submitting)
        {
            _isSubmitting = submitting;
            FormHelpers.SetSubmitting(_registerButton, _registerButtonLabel, _registerButtonSpinner, submitting);
        }

        private void OnLinkClicked(PointerUpLinkTagEvent evt)
        {
            if (evt.linkID == "login")
                EventBus.Publish(new NavigateToLoginRequestedEvent());
        }

        private void TogglePasswordVisibility()
        {
            if (_passwordField == null) return;
            _passwordField.isPasswordField = !_passwordField.isPasswordField;
            if (_passwordToggleButton != null)
                UpdateToggleButtonIcon(_passwordToggleButton, _passwordField.isPasswordField);
        }

        private void ToggleConfirmPasswordVisibility()
        {
            if (_confirmPasswordField == null) return;
            _confirmPasswordField.isPasswordField = !_confirmPasswordField.isPasswordField;
            if (_confirmPasswordToggleButton != null)
                UpdateToggleButtonIcon(_confirmPasswordToggleButton, _confirmPasswordField.isPasswordField);
        }

        private static void UpdateToggleButtonIcon(Button button, bool isHidden)
        {
            if (isHidden) button.AddToClassList("password-toggle--hidden");
            else button.RemoveFromClassList("password-toggle--hidden");
        }

        private void OnAuthFailed(AuthFailedEvent evt)
        {
            SetSubmitting(false);

            if (evt.FieldErrors != null)
            {
                SetFormError(null);
                SetFieldError(_firstNameField, _firstNameErrorLabel,
                    evt.FieldErrors.TryGetValue("firstName", out var firstNameMsg) ? firstNameMsg : null);
                SetFieldError(_lastNameField, _lastNameErrorLabel,
                    evt.FieldErrors.TryGetValue("lastName", out var lastNameMsg) ? lastNameMsg : null);
                SetFieldError(_cellNumberField, _cellNumberErrorLabel,
                    evt.FieldErrors.TryGetValue("cellNumber", out var cellMsg) ? cellMsg : null);
                SetFieldError(_emailField, _emailErrorLabel,
                    evt.FieldErrors.TryGetValue("email", out var emailMsg) ? emailMsg : null);
                SetFieldError(_passwordField, _passwordErrorLabel,
                    evt.FieldErrors.TryGetValue("password", out var pwMsg) ? pwMsg : null);
                SetFieldError(_confirmPasswordField, _confirmPasswordErrorLabel,
                    evt.FieldErrors.TryGetValue("confirmPassword", out var confirmMsg) ? confirmMsg : null);
            }
            else
            {
                ClearAllErrors();
                SetFormError(evt.Reason);
            }
        }

        private void ClearAllErrors()
        {
            SetFormError(null);
            SetFieldError(_firstNameField, _firstNameErrorLabel, null);
            SetFieldError(_lastNameField, _lastNameErrorLabel, null);
            SetFieldError(_cellNumberField, _cellNumberErrorLabel, null);
            SetFieldError(_emailField, _emailErrorLabel, null);
            SetFieldError(_passwordField, _passwordErrorLabel, null);
            SetFieldError(_confirmPasswordField, _confirmPasswordErrorLabel, null);
        }

        private static void SetFieldError(TextField field, Label errorLabel, string message) =>
            FormHelpers.SetFieldError(field, errorLabel, message);

        private void SetFormError(string message) => FormHelpers.SetMessage(_errorLabel, message);

        private void OnDisable()
        {
            if (_registerButton != null) _registerButton.clicked -= OnRegisterClicked;
            if (_richTextLabel != null)
                _richTextLabel.UnregisterCallback<PointerUpLinkTagEvent>(OnLinkClicked);
            if (_passwordToggleButton != null)
                _passwordToggleButton.clicked -= TogglePasswordVisibility;
            if (_confirmPasswordToggleButton != null)
                _confirmPasswordToggleButton.clicked -= ToggleConfirmPasswordVisibility;

            EventBus.Unsubscribe<AuthFailedEvent>(OnAuthFailed);
        }
    }
}
