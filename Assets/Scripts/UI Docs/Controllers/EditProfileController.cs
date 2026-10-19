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

        private TextField _firstName, _lastName, _cellNumber, _email, _username;
        private Label _firstNameError, _lastNameError, _cellNumberError, _usernameError, _usernameHint, _statusLabel, _errorLabel, _saveLabel;
        private VisualElement _usernameGroup;
        private Button _backButton, _saveButton;
        private VisualElement _spinner;

        private ICustomerProfileApi _api;
        private bool _loaded;
        private bool _busy;
        // The generated username can be replaced once; these are what the server last told us.
        private string _loadedUsername = string.Empty;
        private bool _canChangeUsername;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;
            var root = uiDocument.rootVisualElement;

            _firstName = root.Q<TextField>("first-name-field");
            _lastName = root.Q<TextField>("last-name-field");
            _cellNumber = root.Q<TextField>("cell-number-field");
            _email = root.Q<TextField>("email-field");
            _username = root.Q<TextField>("username-field");
            _usernameGroup = root.Q<VisualElement>("username-group");
            _usernameError = root.Q<Label>("username-error-label");
            _usernameHint = root.Q<Label>("username-hint-label");
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
                ApplyUsername(result.Value.username, result.Value.canChangeUsername);
            });
        }

        // Shows the username box: editable while the one change is unused, read-only (like the email) afterwards, and hidden
        // when talking to a server that predates usernames.
        private void ApplyUsername(string username, bool canChange)
        {
            _loadedUsername = ProfileValidation.NormalizeUsername(username);
            _canChangeUsername = canChange;
            if (_usernameGroup != null)
                _usernameGroup.style.display = string.IsNullOrEmpty(_loadedUsername) ? DisplayStyle.None : DisplayStyle.Flex;
            if (_username == null) return;

            _username.SetValueWithoutNotify(_loadedUsername);
            _username.isReadOnly = !canChange;
            _username.EnableInClassList("input-field--readonly", !canChange);
            if (_usernameHint != null)
                _usernameHint.text = canChange
                    ? "Made for you. You can sign in with it or your email, and change it once."
                    : "You have used your one change, so your username stays as it is.";
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

            var changingUsername = _canChangeUsername && _username != null
                && ProfileValidation.NormalizeUsername(_username.value) != _loadedUsername;
            var usernameProblem = changingUsername ? ProfileValidation.ValidateUsername(_username.value) : null;
            FormHelpers.SetFieldError(_username, _usernameError, usernameProblem);
            if (errors.Count > 0 || usernameProblem != null) return;

            if (!TryGetApi(out var token)) return;
            SetBusy(true);

            if (!changingUsername)
            {
                SaveDetails(token);
                return;
            }

            // The username goes first: if it is refused (taken, reserved) nothing else is saved and the box says why.
            _api.ChangeUsername(new ChangeUsernameRequest { username = ProfileValidation.NormalizeUsername(_username.value) }, token, usernameResult =>
            {
                if (this == null) return;
                if (!usernameResult.Success)
                {
                    SetBusy(false);
                    Debug.LogWarning($"{LogPrefix} Username change failed. Status {usernameResult.Error.StatusCode}, code {usernameResult.Error.ErrorCode ?? "unknown"}.");
                    ShowUsernameError(usernameResult.Error.ErrorCode, usernameResult.Error.UserMessage);
                    return;
                }
                ApplyUsername(usernameResult.Value.username, usernameResult.Value.canChangeUsername);
                SaveDetails(token);
            });
        }

        private void ShowUsernameError(string errorCode, string message)
        {
            switch (errorCode)
            {
                case "username_taken":
                case "username_invalid":
                case "username_change_used":
                    FormHelpers.SetFieldError(_username, _usernameError, message);
                    break;
                default:
                    FormHelpers.SetMessage(_errorLabel, message);
                    break;
            }
        }

        // The busy state is already on when this runs.
        private void SaveDetails(string token)
        {
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
