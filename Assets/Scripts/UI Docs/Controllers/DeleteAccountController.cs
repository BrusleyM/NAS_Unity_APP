using UnityEngine;
using UnityEngine.UIElements;
using NAS.Core;
using NAS.Core.Auth;
using NAS.Core.Auth.Dtos;
using NAS.Core.Events;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// Delete my account (Privacy screen). POPIA's right to erasure, and the in-app deletion Apple and Google require. Needs the password, so a
    /// phone left signed in cannot be used to delete the account. Immediate and permanent: on success the customer is signed out and the sign-in
    /// screen says what was kept (enquiries without their details, and any sale a dealership has to keep for a while).
    /// </summary>
    public class DeleteAccountController : MonoBehaviour
    {
        private const string LogPrefix = "[NAS Delete account]";

        private TextField _password;
        private Label _passwordError, _errorLabel, _explanationLabel, _deleteLabel;
        private Button _backButton, _deleteButton;
        private VisualElement _spinner;

        private ICustomerProfileApi _api;
        private bool _busy;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;
            var root = uiDocument.rootVisualElement;

            _password = root.Q<TextField>("delete-password-field");
            _passwordError = root.Q<Label>("delete-password-error-label");
            _errorLabel = root.Q<Label>("profile-error-label");
            _explanationLabel = root.Q<Label>("delete-explanation-label");
            _backButton = root.Q<Button>("profile-back-button");
            _deleteButton = root.Q<Button>("profile-delete-button");
            _deleteLabel = root.Q<Label>("profile-delete-button-label");
            _spinner = root.Q<VisualElement>("profile-delete-button-spinner");

            // Set in code: the UXML is-password attribute is not reliable (LoginCardController does the same).
            if (_password != null) _password.isPasswordField = true;
            // The wording is DataRights' (one tested source), not a copy in the UXML.
            if (_explanationLabel != null) _explanationLabel.text = DataRights.DeleteExplanation;

            if (_backButton != null) _backButton.clicked += OnBackClicked;
            if (_deleteButton != null) _deleteButton.clicked += OnDeleteClicked;
        }

        private void OnDisable()
        {
            if (_backButton != null) _backButton.clicked -= OnBackClicked;
            if (_deleteButton != null) _deleteButton.clicked -= OnDeleteClicked;
        }

        private void OnDeleteClicked()
        {
            if (_busy) return;
            FormHelpers.SetFieldError(_password, _passwordError, null);
            FormHelpers.SetMessage(_errorLabel, null);

            var problem = DataRights.ValidateDeletionPassword(_password.value);
            if (problem != null)
            {
                FormHelpers.SetFieldError(_password, _passwordError, problem);
                return;
            }

            var token = GameManager.Instance != null ? GameManager.Instance.AccessToken : null;
            var resolved = EnvironmentResolver.Resolve(LogPrefix);
            if (resolved.Settings == null || string.IsNullOrEmpty(token))
            {
                FormHelpers.SetMessage(_errorLabel, "Please sign in again.");
                return;
            }

            _api ??= new CustomerProfileApi(this, resolved.Settings, resolved.TrustAnyCertificate);
            SetBusy(true);
            _api.DeleteAccount(new DeleteAccountRequest { password = _password.value }, token, result =>
            {
                if (this == null) return;
                SetBusy(false);
                if (!result.Success)
                {
                    Debug.LogWarning($"{LogPrefix} Delete failed. Status {result.Error.StatusCode}, code {result.Error.ErrorCode ?? "unknown"}.");
                    ShowServerError(result.Error.ErrorCode, result.Error.UserMessage);
                    return;
                }

                _password.value = string.Empty;
                var summary = DataRights.DescribeDeletion(result.Value.leadsAnonymised, result.Value.soldRecordsKept, result.Value.soldRecordsKeptUntil);
                // Order matters: GameManager must stop sending telemetry for the deleted account before the sign-out, and the sign-in
                // screen exists (SessionEndedEvent) before it is told what to say.
                EventBus.Publish(new AccountDeletedEvent());
                EventBus.Publish(new LogoutRequestedEvent());
                EventBus.Publish(new LoginNoticeEvent(summary));   // the green notice, not the red error
            });
        }

        private void ShowServerError(string errorCode, string message)
        {
            if (errorCode == "invalid_password") FormHelpers.SetFieldError(_password, _passwordError, message);
            else FormHelpers.SetMessage(_errorLabel, message);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            FormHelpers.SetSubmitting(_deleteButton, _deleteLabel, _spinner, busy);
        }

        private void OnBackClicked()
        {
            if (!_busy) EventBus.Publish(new CloseProfileScreenRequestedEvent());
        }
    }
}
