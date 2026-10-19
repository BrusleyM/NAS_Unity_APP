using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;
using NAS.Core.Events;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// "Forgot password?": ask for a code by email or username. The server answers the same whether or not the
    /// account exists, so on success the router just moves to the code screen: this card never says which.
    /// </summary>
    public class ForgotPasswordCardController : MonoBehaviour
    {
        private TextField _identifierField;
        private Label _identifierErrorLabel, _errorLabel, _sendLabel, _backLink;
        private Button _sendButton;
        private VisualElement _spinner;
        private bool _submitting;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;
            var root = uiDocument.rootVisualElement;

            _identifierField = root.Q<TextField>("identifier-field");
            _identifierErrorLabel = root.Q<Label>("identifier-error-label");
            _errorLabel = root.Q<Label>("forgot-error-label");
            _sendButton = root.Q<Button>("send-code-button");
            _sendLabel = root.Q<Label>("send-code-button-label");
            _spinner = root.Q<VisualElement>("send-code-button-spinner");
            _backLink = root.Q<Label>("back-link-label");

            if (_sendButton != null) _sendButton.clicked += OnSendClicked;
            if (_backLink != null) _backLink.RegisterCallback<PointerUpLinkTagEvent>(OnLinkClicked);
            EventBus.Subscribe<AuthFailedEvent>(OnAuthFailed);
        }

        /// <summary>Called right after the card is shown: carries over what was typed in the login box.</summary>
        public void Initialize(string prefill)
        {
            if (_identifierField != null && !string.IsNullOrWhiteSpace(prefill))
                _identifierField.SetValueWithoutNotify(prefill.Trim());
        }

        private void OnSendClicked()
        {
            if (_submitting) return;
            FormHelpers.SetFieldError(_identifierField, _identifierErrorLabel, null);
            FormHelpers.SetMessage(_errorLabel, null);
            SetSubmitting(true);
            EventBus.Publish(new PasswordResetCodeRequestedEvent(_identifierField?.value));
        }

        // On success this card is replaced by the code screen, so only the failure path turns the spinner back off.
        private void OnAuthFailed(AuthFailedEvent evt)
        {
            SetSubmitting(false);
            if (evt.FieldErrors != null)
            {
                FormHelpers.SetMessage(_errorLabel, null);
                FormHelpers.SetFieldError(_identifierField, _identifierErrorLabel,
                    evt.FieldErrors.TryGetValue("email", out var message) ? message : null);
            }
            else
            {
                FormHelpers.SetFieldError(_identifierField, _identifierErrorLabel, null);
                FormHelpers.SetMessage(_errorLabel, evt.Reason);
            }
        }

        private void SetSubmitting(bool submitting)
        {
            _submitting = submitting;
            FormHelpers.SetSubmitting(_sendButton, _sendLabel, _spinner, submitting);
        }

        private void OnLinkClicked(PointerUpLinkTagEvent evt)
        {
            if (evt.linkID == "login")
                EventBus.Publish(new NavigateToLoginRequestedEvent());
        }

        private void OnDisable()
        {
            if (_sendButton != null) _sendButton.clicked -= OnSendClicked;
            if (_backLink != null) _backLink.UnregisterCallback<PointerUpLinkTagEvent>(OnLinkClicked);
            EventBus.Unsubscribe<AuthFailedEvent>(OnAuthFailed);
        }
    }
}
