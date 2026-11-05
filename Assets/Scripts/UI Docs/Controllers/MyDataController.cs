using UnityEngine;
using UnityEngine.UIElements;
using NAS.Core;
using NAS.Core.Auth;
using NAS.Core.Auth.Dtos;
using NAS.Core.Events;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// Download my data (Privacy screen). POPIA's right of access: asks the server for a copy of everything held about the customer, says in a
    /// sentence what it holds, and copies it, as the server wrote it, to the clipboard to paste into a note or an email (a phone has no
    /// simple way to save or share a text file from the app).
    /// </summary>
    public class MyDataController : MonoBehaviour
    {
        private const string LogPrefix = "[NAS My data]";

        private Label _summaryLabel, _statusLabel, _errorLabel, _getLabel;
        private Button _backButton, _getButton, _copyButton;
        private VisualElement _spinner;

        private ICustomerProfileApi _api;
        private string _json;
        private bool _busy;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;
            var root = uiDocument.rootVisualElement;

            _summaryLabel = root.Q<Label>("mydata-summary-label");
            _statusLabel = root.Q<Label>("profile-status-label");
            _errorLabel = root.Q<Label>("profile-error-label");
            _backButton = root.Q<Button>("profile-back-button");
            _getButton = root.Q<Button>("mydata-get-button");
            _getLabel = root.Q<Label>("mydata-get-button-label");
            _spinner = root.Q<VisualElement>("mydata-get-button-spinner");
            _copyButton = root.Q<Button>("mydata-copy-button");

            if (_backButton != null) _backButton.clicked += OnBackClicked;
            if (_getButton != null) _getButton.clicked += OnGetClicked;
            if (_copyButton != null) _copyButton.clicked += OnCopyClicked;
        }

        private void OnDisable()
        {
            if (_backButton != null) _backButton.clicked -= OnBackClicked;
            if (_getButton != null) _getButton.clicked -= OnGetClicked;
            if (_copyButton != null) _copyButton.clicked -= OnCopyClicked;
        }

        private void OnGetClicked()
        {
            if (_busy) return;
            FormHelpers.SetMessage(_errorLabel, null);
            FormHelpers.SetMessage(_statusLabel, null);

            var token = GameManager.Instance != null ? GameManager.Instance.AccessToken : null;
            var resolved = EnvironmentResolver.Resolve(LogPrefix);
            if (resolved.Settings == null || string.IsNullOrEmpty(token))
            {
                FormHelpers.SetMessage(_errorLabel, "Please sign in again.");
                return;
            }

            _api ??= new CustomerProfileApi(this, resolved.Settings, resolved.TrustAnyCertificate);
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

                _json = result.Value;
                var summary = Describe(_json);
                if (_summaryLabel != null) { _summaryLabel.text = summary; _summaryLabel.style.display = DisplayStyle.Flex; }
                _copyButton?.SetEnabled(true);
                if (_copyButton != null) _copyButton.style.display = DisplayStyle.Flex;
            });
        }

        private static string Describe(string json)
        {
            var export = JsonUtility.FromJson<DataExportDto>(json) ?? new DataExportDto();
            return DataRights.DescribeExport(export.leads?.Count ?? 0, export.savedBuilds?.Count ?? 0, export.ActivityRecordCount, export.activity != null && export.activity.truncated);
        }

        private void OnCopyClicked()
        {
            if (string.IsNullOrEmpty(_json)) return;
            GUIUtility.systemCopyBuffer = _json;
            FormHelpers.SetMessage(_statusLabel, "Copied. Paste it into a note or an email to keep it.");
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            FormHelpers.SetSubmitting(_getButton, _getLabel, _spinner, busy);
        }

        private void OnBackClicked() => EventBus.Publish(new CloseProfileScreenRequestedEvent());
    }
}
