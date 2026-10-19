using UnityEngine;
using UnityEngine.UIElements;
using NAS.Core.Events;
using NAS.Core.Motion;

namespace NAS.Core
{
    // Lives on a child of the persistent "Game Manager" GameObject
    // (DontDestroyOnLoad), not on a per-scene UI document - unlike the
    // Login/CarSelection/Estimator cards or ArViewportController, this
    // single instance survives every scene load, so any async process
    // anywhere in the app can show/hide the same overlay via
    // LoadingStartedEvent/LoadingFinishedEvent regardless of which scene
    // is currently active.
    public class LoadingOverlayController : MonoBehaviour
    {
        private const string DefaultMessage = "Loading...";

        private VisualElement _root;
        private VisualElement _spinner;
        private Label _messageLabel;
        private bool _visible;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;

            var rootVisualElement = uiDocument.rootVisualElement;
            _root = rootVisualElement.Q<VisualElement>("loading-overlay-root");
            _spinner = rootVisualElement.Q<VisualElement>("loading-spinner");
            _messageLabel = rootVisualElement.Q<Label>("loading-message-label");

            SetVisible(false);

            EventBus.Subscribe<LoadingStartedEvent>(OnLoadingStarted);
            EventBus.Subscribe<LoadingFinishedEvent>(OnLoadingFinished);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LoadingStartedEvent>(OnLoadingStarted);
            EventBus.Unsubscribe<LoadingFinishedEvent>(OnLoadingFinished);
        }

        private void OnLoadingStarted(LoadingStartedEvent evt)
        {
            if (_messageLabel != null)
                _messageLabel.text = string.IsNullOrEmpty(evt.Message) ? DefaultMessage : evt.Message;
            SetVisible(true);
        }

        private void OnLoadingFinished(LoadingFinishedEvent evt) => SetVisible(false);

        private void SetVisible(bool visible)
        {
            _visible = visible;
            if (visible) UiMotion.Spin(_spinner);
            else UiMotion.StopSpin(_spinner);
            if (_root == null) return;
            if (visible)
            {
                _root.style.display = DisplayStyle.Flex;
                UiMotion.FadeIn(_root, 0.18f);
            }
            else
            {
                // Fade out, then take it out of the layout so it stops catching taps.
                UiMotion.FadeOut(_root, 0.18f, () => { if (!_visible) _root.style.display = DisplayStyle.None; });
            }
        }
    }
}
