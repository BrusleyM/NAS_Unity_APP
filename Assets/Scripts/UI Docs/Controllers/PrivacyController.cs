using UnityEngine;
using UnityEngine.UIElements;
using NAS.Core.Events;

namespace NAS.UI.Controllers
{
    /// <summary>Privacy (side menu): static text, only a Back button to wire.</summary>
    public class PrivacyController : MonoBehaviour
    {
        private Button _backButton;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;
            _backButton = uiDocument.rootVisualElement.Q<Button>("profile-back-button");
            if (_backButton != null) _backButton.clicked += OnBackClicked;
        }

        private void OnDisable()
        {
            if (_backButton != null) _backButton.clicked -= OnBackClicked;
        }

        private static void OnBackClicked() => EventBus.Publish(new CloseProfileScreenRequestedEvent());
    }
}
