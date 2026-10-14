using UnityEngine;
using UnityEngine.UIElements;
using NAS.Core;
using NAS.Core.Auth;
using NAS.Core.Auth.Dtos;
using NAS.Core.Events;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// Edit Profile (side menu). Loads the customer's own details from the server, lets them change first name, last
    /// name and cell number (the email is shown read-only), validates client-side first and then saves. Announces what
    /// happened; ParentPageController decides where to go next, same as every other screen.
    /// </summary>
    public class EditProfileController : MonoBehaviour
    {
        private const string LogPrefix = "[NAS Profile]";
        private const float ReturnDelayMs = 900f;

        private TextField _firstName, _lastName, _cellNumber, _email;
        private Label _firstNameError, _lastNameError, _cellNumberError, _statusLabel, _errorLabel, _saveLabel;
        private Button _backButton, _saveButton;
        private VisualElement _spinner;

        private ICustomerProfileApi _api;
        private bool _loaded;
        private bool _busy;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;
            var root = uiDocument.rootVisualElement;

            _firstName = root.Q<TextField>("first-name-field");
            _lastName = root.Q<TextField>("last-name-field");
            _cellNumber = root.Q<TextField>("cell-number-field");
            _email = root.Q<TextField>("email-field");
            _firstNameError = root.Q<Label>("first-name-error-label");
            _lastNameError = root.Q<Label>("last-name-error-label");
            _cellNumberError = root.Q<Label>("cell-number-error-label");
            _statusLabel = root.Q<Label>("profile-status-label");
            _errorLabel = root.Q<Label>("profile-error-label");
            _backButton = root.Q<Button>("profile-back-button");
            _saveButton = root.Q<Button>("profile-save-button");
            _saveLabel = root.Q<Label>("profile-save-button-label");
            _spinner = root.Q<VisualElement>("profile-save-button-spinner");

            if (_backButton != null) _backButton.clicked += OnBackClicked;
            if (_saveButton != null) _saveButton.clicked += OnSaveClicked;
            if (_email != null) _email.isReadOnly = true;

            Load();
        }

        private void OnDisable()
        {
            if (_backButton != null) _backButton.clicked -= OnBackClicked;
            if (_saveButton != null) _saveButton.clicked -= OnSaveClicked;
        }

        private bool TryGetApi(out string accessToken)
        {
            accessToken = GameManager.Instance != null ? GameManager.Instance.AccessToken : null;
            var resolved = EnvironmentResolver.Resolve(LogPrefix);
            if (resolved.Settings == null || string.IsNullOrEmpty(accessToken))
            {
                FormHelpers.SetMessage(_errorLabel, "Please sign in again.");
                return false;
            }
            _api ??= new CustomerProfileApi(this, resolved.Settings, resolved.TrustAnyCertificate);
            return true;
        }

        private void Load()
        {
            if (_busy || !TryGetApi(out var token)) return;
            SetBusy(true);
            FormHelpers.SetMessage(_errorLabel, null);
            _api.GetProfile(token, result =>
            {
                if (this == null) return; // the screen may have been swapped out while the request was in flight
                SetBusy(false);
                if (!result.Success)
                {
                    Debug.LogWarning($"{LogPrefix} Load failed: {result.Error.Detail}");
                    FormHelpers.SetMessage(_errorLabel, "Couldn't load your details. Tap Save to try again.");
                    return;
                }
                _loaded = true;
                _firstName.value = result.Value.firstName;
                _lastName.value = result.Value.lastName;
                _cellNumber.value = result.Value.cellNumber;
                _email.value = result.Value.email;
            });
        }

        private void OnSaveClicked()
        {
            if (_busy) return;
            if (!_loaded) { Load(); return; }

            FormHelpers.SetMessage(_errorLabel, null);
            FormHelpers.SetMessage(_statusLabel, null);

            var errors = ProfileValidation.ValidateProfile(_firstName.value, _lastName.value, _cellNumber.value);
            FormHelpers.SetFieldError(_firstName, _firstNameError, FormHelpers.ErrorFor(errors, "firstName"));
            FormHelpers.SetFieldError(_lastName, _lastNameError, FormHelpers.ErrorFor(errors, "lastName"));
            FormHelpers.SetFieldError(_cellNumber, _cellNumberError, FormHelpers.ErrorFor(errors, "cellNumber"));
            if (errors.Count > 0) return;

            if (!TryGetApi(out var token)) return;
            SetBusy(true);
            var request = new UpdateProfileRequest
            {
                firstName = _firstName.value.Trim(),
                lastName = _lastName.value.Trim(),
                cellNumber = _cellNumber.value.Trim()
            };
            _api.UpdateProfile(request, token, result =>
            {
                if (this == null) return;
                SetBusy(false);
                if (!result.Success)
                {
                    Debug.LogWarning($"{LogPrefix} Save failed. Status {result.Error.StatusCode}, code {result.Error.ErrorCode ?? "unknown"}.");
                    FormHelpers.SetMessage(_errorLabel, result.Error.UserMessage);
                    return;
                }

                EventBus.Publish(new ProfileUpdatedEvent(result.Value.firstName, result.Value.lastName));
                FormHelpers.SetMessage(_statusLabel, "Profile updated.");
                _saveButton?.SetEnabled(false);
                // A short beat so the confirmation is readable, then back to where the menu was opened.
                _statusLabel?.schedule.Execute(() => EventBus.Publish(new CloseProfileScreenRequestedEvent())).ExecuteLater((long)ReturnDelayMs);
            });
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            FormHelpers.SetSubmitting(_saveButton, _saveLabel, _spinner, busy);
        }

        private void OnBackClicked() => EventBus.Publish(new CloseProfileScreenRequestedEvent());
    }
}
