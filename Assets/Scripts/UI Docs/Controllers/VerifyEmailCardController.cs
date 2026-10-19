using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using NAS.Core;
using NAS.Core.Events;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// "Verify your email": the customer types the six-digit code we emailed. A correct code signs them straight in
    /// (AuthController publishes the usual AuthSucceededEvent), so there is no success handler here: the router moves on.
    /// Purely a view: it publishes what the customer did and shows what comes back.
    /// </summary>
    public class VerifyEmailCardController : MonoBehaviour
    {
        private TextField _codeField;
        private Label _subtitleLabel, _codeErrorLabel, _noticeLabel, _errorLabel, _verifyLabel, _backLink;
        private Button _verifyButton, _resendButton;
        private VisualElement _spinner;

        private CodeCardSupport.ResendButton _resend;
        private string _identifier = string.Empty;
        private bool _verifying;
        private bool _resending;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;
            var root = uiDocument.rootVisualElement;

            _codeField = root.Q<TextField>("code-field");
            _subtitleLabel = root.Q<Label>("verify-subtitle-label");
            _codeErrorLabel = root.Q<Label>("code-error-label");
            _noticeLabel = root.Q<Label>("verify-notice-label");
            _errorLabel = root.Q<Label>("verify-error-label");
            _verifyButton = root.Q<Button>("verify-button");
            _verifyLabel = root.Q<Label>("verify-button-label");
            _spinner = root.Q<VisualElement>("verify-button-spinner");
            _resendButton = root.Q<Button>("resend-button");
            _backLink = root.Q<Label>("back-link-label");

            CodeCardSupport.ConfigureCodeField(_codeField);
            _resend = new CodeCardSupport.ResendButton(_resendButton);

            if (_verifyButton != null) _verifyButton.clicked += OnVerifyClicked;
            if (_resendButton != null) _resendButton.clicked += OnResendClicked;
            if (_backLink != null) _backLink.RegisterCallback<PointerUpLinkTagEvent>(OnLinkClicked);

            EventBus.Subscribe<AuthFailedEvent>(OnAuthFailed);
            EventBus.Subscribe<CodeSentEvent>(OnCodeSent);
        }

        /// <summary>Called by ParentPageController right after the card is shown. A code was just emailed, so the countdown starts now.</summary>
        public void Initialize(string identifier, string email)
        {
            _identifier = identifier ?? string.Empty;
            if (_subtitleLabel != null)
            {
                var where = string.IsNullOrEmpty(email) ? "the email on your account" : AccountRecovery.MaskEmail(email);
                _subtitleLabel.text = $"We sent a 6-digit code to {where}. It expires in 10 minutes.";
            }
            _resend?.Restart();
        }

        private void OnVerifyClicked()
        {
            if (_verifying) return;
            ClearMessages();
            SetVerifying(true);
            EventBus.Publish(new VerifyEmailRequestedEvent(_identifier, _codeField?.value));
        }

        private void OnResendClicked()
        {
            if (_resending) return;
            ClearMessages();
            _resending = true;
            _resendButton?.SetEnabled(false);
            EventBus.Publish(new ResendVerificationRequestedEvent(_identifier));
        }

        private void OnCodeSent(CodeSentEvent evt)
        {
            _resending = false;
            FormHelpers.SetMessage(_noticeLabel, "We have sent you a new code.");
            if (_codeField != null) _codeField.SetValueWithoutNotify(string.Empty);
            _resend?.Restart();
        }

        private void OnAuthFailed(AuthFailedEvent evt)
        {
            SetVerifying(false);
            if (_resending)
            {
                _resending = false;
                _resend?.Restart();   // the countdown decides when it can be tried again
            }

            if (evt.FieldErrors != null)
            {
                FormHelpers.SetMessage(_errorLabel, null);
                FormHelpers.SetFieldError(_codeField, _codeErrorLabel,
                    evt.FieldErrors.TryGetValue("code", out var message) ? message : null);
            }
            else
            {
                FormHelpers.SetFieldError(_codeField, _codeErrorLabel, null);
                FormHelpers.SetMessage(_errorLabel, evt.Reason);
            }
        }

        private void ClearMessages()
        {
            FormHelpers.SetFieldError(_codeField, _codeErrorLabel, null);
            FormHelpers.SetMessage(_errorLabel, null);
            FormHelpers.SetMessage(_noticeLabel, null);
        }

        private void SetVerifying(bool verifying)
        {
            _verifying = verifying;
            FormHelpers.SetSubmitting(_verifyButton, _verifyLabel, _spinner, verifying);
        }

        private void OnLinkClicked(PointerUpLinkTagEvent evt)
        {
            if (evt.linkID == "login")
                EventBus.Publish(new NavigateToLoginRequestedEvent());
        }

        private void OnDisable()
        {
            _resend?.Stop();
            if (_verifyButton != null) _verifyButton.clicked -= OnVerifyClicked;
            if (_resendButton != null) _resendButton.clicked -= OnResendClicked;
            if (_backLink != null) _backLink.UnregisterCallback<PointerUpLinkTagEvent>(OnLinkClicked);
            EventBus.Unsubscribe<AuthFailedEvent>(OnAuthFailed);
            EventBus.Unsubscribe<CodeSentEvent>(OnCodeSent);
        }
    }
}
