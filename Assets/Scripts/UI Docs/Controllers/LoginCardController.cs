using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using NAS.Core.Events;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// Purely a view. It reads the login form and publishes what happened —
    /// it doesn't know or care who (AuthController) handles it, or who
    /// (ParentPageController) navigates away afterwards.
    /// </summary>
    public class LoginCardController : MonoBehaviour
    {
        [SerializeField] private TextField _emailField;
        [SerializeField] private TextField _passwordField;
        [SerializeField] private Button _loginButton;
        [SerializeField] private Label _loginButtonLabel;
        [SerializeField] private VisualElement _loginButtonSpinner;
        [SerializeField] private Label _richTextLabel;
        [SerializeField] private Label _errorLabel; // general/backend-level error, "login-error-label"
        [SerializeField] private Label _noticeLabel; // a positive message, "login-notice-label" (e.g. password changed)
        private Label _forgotLinkLabel;
        [SerializeField] private Label _emailErrorLabel;
        [SerializeField] private Label _passwordErrorLabel;
        [SerializeField] private Button _passwordToggleButton;

        private bool _isSubmitting;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;

            var root = uiDocument.rootVisualElement;

            _emailField = root.Q<TextField>("email-field");
            _passwordField = root.Q<TextField>("password-field");
            _loginButton = root.Q<Button>("login-button");
            _loginButtonLabel = root.Q<Label>("login-button-label");
            _loginButtonSpinner = root.Q<VisualElement>("login-button-spinner");
            _richTextLabel = root.Q<Label>("register-link-label");
            _errorLabel = root.Q<Label>("login-error-label");
            _noticeLabel = root.Q<Label>("login-notice-label");
            _forgotLinkLabel = root.Q<Label>("forgot-link-label");
            _emailErrorLabel = root.Q<Label>("email-error-label");
            _passwordErrorLabel = root.Q<Label>("password-error-label");
            _passwordToggleButton = root.Q<Button>("password-toggle-button");

            if (_loginButton != null)
                _loginButton.clicked += OnLoginClicked;

            if (_richTextLabel != null)
                _richTextLabel.RegisterCallback<PointerUpLinkTagEvent>(OnLinkClicked);

            if (_forgotLinkLabel != null)
                _forgotLinkLabel.RegisterCallback<PointerUpLinkTagEvent>(OnForgotLinkClicked);

            if (_passwordToggleButton != null)
            {
                _passwordToggleButton.clicked += TogglePasswordVisibility;
                // Set initial state
                _passwordToggleButton.text = string.Empty;
                UpdateToggleButtonIcon(_passwordToggleButton, true);
                if (_passwordField != null) _passwordField.isPasswordField = true;
            }

            EventBus.Subscribe<AuthFailedEvent>(OnAuthFailed);
            EventBus.Subscribe<SessionExpiredEvent>(OnSessionExpired);
            EventBus.Subscribe<LoginNoticeEvent>(OnLoginNotice);
        }

        private void OnForgotLinkClicked(PointerUpLinkTagEvent evt)
        {
            if (evt.linkID == "forgot")
                EventBus.Publish(new NavigateToForgotPasswordRequestedEvent(_emailField?.value));
        }

        private void OnLoginNotice(LoginNoticeEvent evt) => FormHelpers.SetMessage(_noticeLabel, evt.Message);

        private void OnLoginClicked()
        {
            if (_isSubmitting) return; // Button is disabled while submitting, but guard anyway
            ClearAllErrors();
            SetSubmitting(true);
            EventBus.Publish(new LoginRequestedEvent(_emailField?.value, _passwordField?.value));
        }

        // No explicit "success" handler needed here - on success this whole
        // card gets destroyed by ParentPageController's navigation (see
        // SessionAuthenticatedEvent in GameEvents.cs), so there's nothing to
        // revert the button back to. Only the failure path needs to turn the
        // spinner back off, since the form stays up for the user to retry.
        private void SetSubmitting(bool submitting)
        {
            _isSubmitting = submitting;
            FormHelpers.SetSubmitting(_loginButton, _loginButtonLabel, _loginButtonSpinner, submitting);
        }

        private void OnLinkClicked(PointerUpLinkTagEvent evt)
        {
            if (evt.linkID == "register")
                EventBus.Publish(new NavigateToRegisterRequestedEvent());
        }

        private void TogglePasswordVisibility()
        {
            if (_passwordField == null) return;
            _passwordField.isPasswordField = !_passwordField.isPasswordField;
            if (_passwordToggleButton != null)
                UpdateToggleButtonIcon(_passwordToggleButton, _passwordField.isPasswordField);
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
                SetFieldError(_emailField, _emailErrorLabel,
                    evt.FieldErrors.TryGetValue("email", out var emailMsg) ? emailMsg : null);
                SetFieldError(_passwordField, _passwordErrorLabel,
                    evt.FieldErrors.TryGetValue("password", out var pwMsg) ? pwMsg : null);
            }
            else
            {
                ClearAllErrors();
                SetFormError(evt.Reason);
            }
        }

        private void OnSessionExpired(SessionExpiredEvent evt)
        {
            SetSubmitting(false);
            SetFormError(evt.Message);
        }

        private void ClearAllErrors()
        {
            FormHelpers.SetMessage(_noticeLabel, null);
            SetFormError(null);
            SetFieldError(_emailField, _emailErrorLabel, null);
            SetFieldError(_passwordField, _passwordErrorLabel, null);
        }

        private static void SetFieldError(TextField field, Label errorLabel, string message) =>
            FormHelpers.SetFieldError(field, errorLabel, message);

        private void SetFormError(string message) => FormHelpers.SetMessage(_errorLabel, message);

        private void OnDisable()
        {
            if (_loginButton != null) _loginButton.clicked -= OnLoginClicked;
            if (_richTextLabel != null)
                _richTextLabel.UnregisterCallback<PointerUpLinkTagEvent>(OnLinkClicked);
            if (_forgotLinkLabel != null)
                _forgotLinkLabel.UnregisterCallback<PointerUpLinkTagEvent>(OnForgotLinkClicked);
            if (_passwordToggleButton != null)
                _passwordToggleButton.clicked -= TogglePasswordVisibility;

            EventBus.Unsubscribe<AuthFailedEvent>(OnAuthFailed);
            EventBus.Unsubscribe<SessionExpiredEvent>(OnSessionExpired);
            EventBus.Unsubscribe<LoginNoticeEvent>(OnLoginNotice);
        }
    }
}
