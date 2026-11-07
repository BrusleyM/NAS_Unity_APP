using UnityEngine;
using UnityEngine.UIElements;
using NAS.Core.Events;

namespace NAS.UI.Controllers
{
    /// <summary>Privacy (side menu): the plain-language text, a Back button, and the two buttons for the customer's rights (a copy of their data, deleting the account).</summary>
    public class PrivacyController : MonoBehaviour
    {
        private Button _backButton, _myDataButton, _deleteButton;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;
            var root = uiDocument.rootVisualElement;
            _backButton = root.Q<Button>("profile-back-button");
            _myDataButton = root.Q<Button>("privacy-my-data-button");
            _deleteButton = root.Q<Button>("privacy-delete-account-button");
            if (_backButton != null) _backButton.clicked += OnBackClicked;
            if (_myDataButton != null) _myDataButton.clicked += OnMyDataClicked;
            if (_deleteButton != null) _deleteButton.clicked += OnDeleteClicked;
        }

        private void OnDisable()
        {
            if (_backButton != null) _backButton.clicked -= OnBackClicked;
            if (_myDataButton != null) _myDataButton.clicked -= OnMyDataClicked;
            if (_deleteButton != null) _deleteButton.clicked -= OnDeleteClicked;
        }

        private static void OnBackClicked() => EventBus.Publish(new CloseProfileScreenRequestedEvent());
        private static void OnMyDataClicked() => EventBus.Publish(new MyDataRequestedEvent());
        private static void OnDeleteClicked() => EventBus.Publish(new DeleteAccountRequestedEvent());
    }
}
