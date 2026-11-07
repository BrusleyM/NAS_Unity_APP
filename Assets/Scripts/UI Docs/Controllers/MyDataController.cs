using UnityEngine;
using UnityEngine.UIElements;
using NAS.Core;
using NAS.Core.Auth;
using NAS.Core.Events;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// Get a copy of my data (Privacy screen). POPIA's right of access: asks the server for a copy of everything held about the customer, says
    /// in a sentence what it holds, and offers to copy it, laid out so it can be read, to the clipboard to paste somewhere safe (a phone has no
    /// simple way to save or share a text file from the app). A copy too large for the clipboard is not offered: the person is told to email us.
    /// The text is dropped when the screen is left.
    /// </summary>
    public class MyDataController : MonoBehaviour
    {
        private const string LogPrefix = "[NAS My data]";

        private Label _summaryLabel, _warningLabel, _statusLabel, _errorLabel, _getLabel;
        private Button _backButton, _getButton, _copyButton, _emailButton;
        private VisualElement _spinner, _emailSpinner;
        private Label _emailLabel;

        private ICustomerProfileApi _api;
        private string _text;
        private bool _busy;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;
            var root = uiDocument.rootVisualElement;

            _summaryLabel = root.Q<Label>("mydata-summary-label");
            _warningLabel = root.Q<Label>("mydata-warning-label");
            _statusLabel = root.Q<Label>("profile-status-label");
            _errorLabel = root.Q<Label>("profile-error-label");
            _backButton = root.Q<Button>("profile-back-button");
            _getButton = root.Q<Button>("mydata-get-button");
            _getLabel = root.Q<Label>("mydata-get-button-label");
            _spinner = root.Q<VisualElement>("mydata-get-button-spinner");
            _copyButton = root.Q<Button>("mydata-copy-button");
            _emailButton = root.Q<Button>("mydata-email-button");
            _emailLabel = root.Q<Label>("mydata-email-button-label");
            _emailSpinner = root.Q<VisualElement>("mydata-email-button-spinner");

            if (_warningLabel != null) _warningLabel.text = DataRights.CopyWarning;   // one tested wording, not a copy in the UXML

            if (_backButton != null) _backButton.clicked += OnBackClicked;
            if (_getButton != null) _getButton.clicked += OnGetClicked;
            if (_copyButton != null) _copyButton.clicked += OnCopyClicked;
            if (_emailButton != null) _emailButton.clicked += OnEmailClicked;
        }

        private void OnDisable()
        {
            if (_backButton != null) _backButton.clicked -= OnBackClicked;
            if (_getButton != null) _getButton.clicked -= OnGetClicked;
            if (_copyButton != null) _copyButton.clicked -= OnCopyClicked;
            if (_emailButton != null) _emailButton.clicked -= OnEmailClicked;
            _text = null;   // personal information is not held longer than the screen needs it
        }

        private void OnGetClicked()
        {
            if (_busy) return;
            FormHelpers.SetMessage(_errorLabel, null);
            FormHelpers.SetMessage(_statusLabel, null);
            ShowCopyControls(false);

            if (!ProfileApiAccess.TryPrepare(this, LogPrefix, ref _api, out var token))
            {
                FormHelpers.SetMessage(_errorLabel, "Please sign in again.");
                return;
            }

            SetBusy(true);
            _api.ExportData(token, result =>
            {
                if (this == null) return;
                SetBusy(false);
                if (!result.Success)
                {
                    Debug.LogWarning($"{LogPrefix} Export failed. Status {result.Error.StatusCode}, code {result.Error.ErrorCode ?? "unknown"}.");
                    FormHelpers.SetMessage(_errorLabel, result.Error.UserMessage);
                    return;
                }

                if (_summaryLabel != null) { _summaryLabel.text = DataExportReader.Describe(result.Value); _summaryLabel.style.display = DisplayStyle.Flex; }

                var readable = DataRights.PrettyPrintJson(result.Value);
                if (DataRights.FitsClipboard(readable))
                {
                    _text = readable;
                    ShowCopyControls(true);
                }
                else
                {
                    _text = null;
                    FormHelpers.SetMessage(_statusLabel, DataRights.TooLargeToCopy);   // not a fault, so not the red error style
                }
            });
        }

        // The server sends the copy to the customer's own verified address; nothing personal comes back to the device but the hidden address.
        // Its sentences for "not verified", "too soon" and "could not send" are written for the customer, so they are shown as they are.
        private void OnEmailClicked()
        {
            if (_busy) return;
            FormHelpers.SetMessage(_errorLabel, null);
            FormHelpers.SetMessage(_statusLabel, null);

            if (!ProfileApiAccess.TryPrepare(this, LogPrefix, ref _api, out var token))
            {
                FormHelpers.SetMessage(_errorLabel, "Please sign in again.");
                return;
            }

            SetBusy(true);
            _api.EmailData(token, result =>
            {
                if (this == null) return;
                SetBusy(false);
                if (!result.Success)
                {
                    Debug.LogWarning($"{LogPrefix} Email copy failed. Status {result.Error.StatusCode}, code {result.Error.ErrorCode ?? "unknown"}.");
                    FormHelpers.SetMessage(_errorLabel, result.Error.UserMessage);
                    return;
                }
                FormHelpers.SetMessage(_statusLabel, DataRights.EmailedCopySent(result.Value?.sentTo));
            });
        }

        private void ShowCopyControls(bool show)
        {
            var display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (_copyButton != null) _copyButton.style.display = display;
            if (_warningLabel != null) _warningLabel.style.display = display;
            if (!show && _summaryLabel != null) _summaryLabel.style.display = DisplayStyle.None;
        }

        private void OnCopyClicked()
        {
            if (string.IsNullOrEmpty(_text)) return;
            GUIUtility.systemCopyBuffer = _text;
            FormHelpers.SetMessage(_statusLabel, "Copied. Paste it somewhere safe, then clear your clipboard.");
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            FormHelpers.SetSubmitting(_getButton, _getLabel, _spinner, busy);
            FormHelpers.SetSubmitting(_emailButton, _emailLabel, _emailSpinner, busy);   // one request at a time: both buttons wait
        }

        private void OnBackClicked() => EventBus.Publish(new CloseProfileScreenRequestedEvent());
    }
}
