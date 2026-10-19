using UnityEngine;
using UnityEngine.UIElements;
using NAS.Core;
using NAS.Core.Auth;
using NAS.Core.Auth.Dtos;
using NAS.Core.Events;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// Change Password (side menu). Needs the current password as well as the new one, so a phone left signed in
    /// can't be used to take over the account. The server's answers are mapped back onto the field they are about.
    /// </summary>
    public class ChangePasswordController : MonoBehaviour
    {
        private const string LogPrefix = "[NAS Password]";
        private const float ReturnDelayMs = 900f;

        private TextField _current, _new, _confirm;
        private Label _currentError, _newError, _confirmError, _statusLabel, _errorLabel, _saveLabel;
        private Button _backButton, _saveButton;
        private VisualElement _spinner;

        private ICustomerProfileApi _api;
        private bool _busy;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;
            var root = uiDocument.rootVisualElement;

            _current = root.Q<TextField>("current-password-field");
            _new = root.Q<TextField>("new-password-field");
            _confirm = root.Q<TextField>("confirm-password-field");
            _currentError = root.Q<Label>("current-password-error-label");
            _newError = root.Q<Label>("new-password-error-label");
            _confirmError = root.Q<Label>("confirm-password-error-label");
            _statusLabel = root.Q<Label>("profile-status-label");
            _errorLabel = root.Q<Label>("profile-error-label");
            _backButton = root.Q<Button>("profile-back-button");
            _saveButton = root.Q<Button>("profile-save-button");
            _saveLabel = root.Q<Label>("profile-save-button-label");
            _spinner = root.Q<VisualElement>("profile-save-button-spinner");

            // Set in code: the UXML is-password attribute is not reliable (LoginCardController does the same).
            if (_current != null) _current.isPasswordField = true;
            if (_new != null) _new.isPasswordField = true;
            if (_confirm != null) _confirm.isPasswordField = true;

            if (_backButton != null) _backButton.clicked += OnBackClicked;
            if (_saveButton != null) _saveButton.clicked += OnSaveClicked;
        }

        private void OnDisable()
        {
            if (_backButton != null) _backButton.clicked -= OnBackClicked;
            if (_saveButton != null) _saveButton.clicked -= OnSaveClicked;
        }

        private void OnSaveClicked()
        {
            if (_busy) return;
            ClearErrors();

            var errors = ProfileValidation.ValidatePasswordChange(_current.value, _new.value, _confirm.value);
            ShowFieldErrors(errors);
            if (errors.Count > 0) return;

            var token = GameManager.Instance != null ? GameManager.Instance.AccessToken : null;
            var resolved = EnvironmentResolver.Resolve(LogPrefix);
            if (resolved.Settings == null || string.IsNullOrEmpty(token))
            {
                FormHelpers.SetMessage(_errorLabel, "Please sign in again.");
                return;
            }

            _api ??= new CustomerProfileApi(this, resolved.Settings, resolved.TrustAnyCertificate);
            SetBusy(true);
            _api.ChangePassword(new ChangePasswordRequest { currentPassword = _current.value, newPassword = _new.value }, token, result =>
            {
                if (this == null) return;
                SetBusy(false);
                if (!result.Success)
                {
                    Debug.LogWarning($"{LogPrefix} Change failed. Status {result.Error.StatusCode}, code {result.Error.ErrorCode ?? "unknown"}.");
                    ShowServerError(result.Error.ErrorCode, result.Error.UserMessage);
                    return;
                }

                _current.value = _new.value = _confirm.value = string.Empty;
                FormHelpers.SetMessage(_statusLabel, "Password changed.");
                _saveButton?.SetEnabled(false);
                _statusLabel?.schedule.Execute(() => EventBus.Publish(new CloseProfileScreenRequestedEvent())).ExecuteLater((long)ReturnDelayMs);
            });
        }

        private void ShowServerError(string errorCode, string message)
        {
            switch (errorCode)
            {
                case "invalid_current_password":
                    FormHelpers.SetFieldError(_current, _currentError, message);
                    break;
                case "password_unchanged":
                    FormHelpers.SetFieldError(_new, _newError, message);
                    break;
                default:
                    FormHelpers.SetMessage(_errorLabel, message);
                    break;
            }
        }

        private void ShowFieldErrors(System.Collections.Generic.IDictionary<string, string> errors)
        {
            FormHelpers.SetFieldError(_current, _currentError, FormHelpers.ErrorFor(errors, "currentPassword"));
            FormHelpers.SetFieldError(_new, _newError, FormHelpers.ErrorFor(errors, "newPassword"));
            FormHelpers.SetFieldError(_confirm, _confirmError, FormHelpers.ErrorFor(errors, "confirmPassword"));
        }

        private void ClearErrors()
        {
            ShowFieldErrors(null);
            FormHelpers.SetMessage(_errorLabel, null);
            FormHelpers.SetMessage(_statusLabel, null);
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            FormHelpers.SetSubmitting(_saveButton, _saveLabel, _spinner, busy);
        }

        private void OnBackClicked() => EventBus.Publish(new CloseProfileScreenRequestedEvent());
    }
}
