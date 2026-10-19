using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using NAS.Core;
using NAS.Core.Events;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// "Choose a new password": the emailed code plus the new password (twice). On success AuthController announces
    /// PasswordResetCompletedEvent and the router returns to the login screen with a notice, so this card has no success path.
    /// "Resend code" asks for another reset code (the same request as the forgot-password screen).
    /// </summary>
    public class ResetPasswordCardController : MonoBehaviour
    {
        private TextField _codeField, _newField, _confirmField;
        private Label _subtitleLabel, _codeError, _newError, _confirmError, _noticeLabel, _errorLabel, _resetLabel, _backLink;
        private Button _resetButton, _resendButton;
        private VisualElement _spinner;

        private CodeCardSupport.ResendButton _resend;
        private string _identifier = string.Empty;
        private bool _submitting;
        private bool _resending;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;
            var root = uiDocument.rootVisualElement;

            _codeField = root.Q<TextField>("code-field");
            _newField = root.Q<TextField>("new-password-field");
            _confirmField = root.Q<TextField>("confirm-password-field");
            _subtitleLabel = root.Q<Label>("reset-subtitle-label");
            _codeError = root.Q<Label>("code-error-label");
            _newError = root.Q<Label>("new-password-error-label");
            _confirmError = root.Q<Label>("confirm-password-error-label");
            _noticeLabel = root.Q<Label>("reset-notice-label");
            _errorLabel = root.Q<Label>("reset-error-label");
            _resetButton = root.Q<Button>("reset-button");
            _resetLabel = root.Q<Label>("reset-button-label");
            _spinner = root.Q<VisualElement>("reset-button-spinner");
            _resendButton = root.Q<Button>("resend-button");
            _backLink = root.Q<Label>("back-link-label");

            // Set in code: the UXML is-password attribute is not reliable (LoginCardController does the same).
            if (_newField != null) _newField.isPasswordField = true;
            if (_confirmField != null) _confirmField.isPasswordField = true;
            CodeCardSupport.ConfigureCodeField(_codeField);
            _resend = new CodeCardSupport.ResendButton(_resendButton);

            if (_resetButton != null) _resetButton.clicked += OnResetClicked;
            if (_resendButton != null) _resendButton.clicked += OnResendClicked;
            if (_backLink != null) _backLink.RegisterCallback<PointerUpLinkTagEvent>(OnLinkClicked);

            EventBus.Subscribe<AuthFailedEvent>(OnAuthFailed);
            EventBus.Subscribe<PasswordResetCodeSentEvent>(OnCodeSent);
        }

        /// <summary>Called right after the card is shown. A code was just requested, so the resend countdown starts now.</summary>
        public void Initialize(string identifier)
        {
            _identifier = identifier ?? string.Empty;
            if (_subtitleLabel != null)
            {
                // The server never says whether the account exists, so neither do we: "if".
                var where = AccountRecovery.LooksLikeEmail(_identifier) ? AccountRecovery.MaskEmail(_identifier) : "your email";
                _subtitleLabel.text = $"If there is an account for that, we sent a 6-digit code to {where}. Enter it with your new password.";
            }
            _resend?.Restart();
        }

        private void OnResetClicked()
        {
            if (_submitting) return;
            ClearMessages();
            SetSubmitting(true);
            EventBus.Publish(new ResetPasswordRequestedEvent(_identifier, _codeField?.value, _newField?.value, _confirmField?.value));
        }

        private void OnResendClicked()
        {
            if (_resending) return;
            ClearMessages();
            _resending = true;
            _resendButton?.SetEnabled(false);
            EventBus.Publish(new PasswordResetCodeRequestedEvent(_identifier));
        }

        private void OnCodeSent(PasswordResetCodeSentEvent evt)
        {
            _resending = false;
            FormHelpers.SetMessage(_noticeLabel, "If there is an account for that, we have sent a new code.");
            if (_codeField != null) _codeField.SetValueWithoutNotify(string.Empty);
            _resend?.Restart();
        }

        private void OnAuthFailed(AuthFailedEvent evt)
        {
            SetSubmitting(false);
            if (_resending)
            {
                _resending = false;
                _resend?.Restart();
            }

            if (evt.FieldErrors != null)
            {
                FormHelpers.SetMessage(_errorLabel, null);
                FormHelpers.SetFieldError(_codeField, _codeError, Field(evt, "code"));
                FormHelpers.SetFieldError(_newField, _newError, Field(evt, "newPassword"));
                FormHelpers.SetFieldError(_confirmField, _confirmError, Field(evt, "confirmPassword"));
            }
            else
            {
                ClearFieldErrors();
                FormHelpers.SetMessage(_errorLabel, evt.Reason);
            }
        }

        private static string Field(AuthFailedEvent evt, string key) =>
            evt.FieldErrors != null && evt.FieldErrors.TryGetValue(key, out var message) ? message : null;

        private void ClearFieldErrors()
        {
            FormHelpers.SetFieldError(_codeField, _codeError, null);
            FormHelpers.SetFieldError(_newField, _newError, null);
            FormHelpers.SetFieldError(_confirmField, _confirmError, null);
        }

        private void ClearMessages()
        {
            ClearFieldErrors();
            FormHelpers.SetMessage(_errorLabel, null);
            FormHelpers.SetMessage(_noticeLabel, null);
        }

        private void SetSubmitting(bool submitting)
        {
            _submitting = submitting;
            FormHelpers.SetSubmitting(_resetButton, _resetLabel, _spinner, submitting);
        }

        private void OnLinkClicked(PointerUpLinkTagEvent evt)
        {
            if (evt.linkID == "login")
                EventBus.Publish(new NavigateToLoginRequestedEvent());
        }

        private void OnDisable()
        {
            _resend?.Stop();
            if (_resetButton != null) _resetButton.clicked -= OnResetClicked;
            if (_resendButton != null) _resendButton.clicked -= OnResendClicked;
            if (_backLink != null) _backLink.UnregisterCallback<PointerUpLinkTagEvent>(OnLinkClicked);
            EventBus.Unsubscribe<AuthFailedEvent>(OnAuthFailed);
            EventBus.Unsubscribe<PasswordResetCodeSentEvent>(OnCodeSent);
        }
    }
}
