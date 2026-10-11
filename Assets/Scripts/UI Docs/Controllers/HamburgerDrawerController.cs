using UnityEngine;
using UnityEngine.UIElements;
using NAS.Core;
using NAS.Core.Events;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// The side menu. Shows who is signed in and which dealership they chose, and announces what was tapped
    /// (Edit Profile, Change Password, Privacy, Change dealership, Sign Out). It does not navigate: ParentPageController
    /// reacts to the events, same as every other screen. It closes itself when anything is tapped, or when the dimmed
    /// area is tapped.
    /// </summary>
    public class HamburgerDrawerController : MonoBehaviour
    {
        private const long CloseAnimationMs = 230;

        private VisualElement _overlay;
        private VisualElement _panel;
        private VisualElement _backdrop;
        private bool _closing;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;
            var root = uiDocument.rootVisualElement;

            _overlay = root.Q<VisualElement>("drawer-overlay");
            _panel = root.Q<VisualElement>("drawer-panel");
            _backdrop = root.Q<VisualElement>("drawer-backdrop");
            if (_overlay == null) return;

            var user = GameManager.Instance != null ? GameManager.Instance.CurrentUser : null;
            var dealership = GameManager.Instance != null ? GameManager.Instance.SelectedDealership : null;
            SetText(root, "drawer-name-label", user?.displayName);
            SetText(root, "drawer-email-label", user?.email);
            SetText(root, "drawer-initials-label", ProfileValidation.Initials(user?.displayName));
            SetText(root, "drawer-dealership-label", dealership != null ? dealership.name : "Not chosen yet");
            SetText(root, "drawer-version-label", $"NeoXR AR v{Application.version}");

            Wire(root, "drawer-edit-profile-button", () => Choose(new EditProfileRequestedEvent()));
            Wire(root, "drawer-change-password-button", () => Choose(new ChangePasswordRequestedEvent()));
            Wire(root, "drawer-privacy-button", () => Choose(new PrivacyRequestedEvent()));
            Wire(root, "drawer-change-dealership-button", () => Choose(new ChangeDealershipRequestedEvent()));
            Wire(root, "drawer-signout-button", () => Choose(new LogoutRequestedEvent()));
            _backdrop?.RegisterCallback<ClickEvent>(OnBackdropClicked);

            // Starts off-screen (the "--closed" classes); removing them one frame later lets the USS transition slide it in.
            _overlay.schedule.Execute(() =>
            {
                _panel?.RemoveFromClassList("drawer-panel--closed");
                _backdrop?.RemoveFromClassList("drawer-backdrop--closed");
            }).ExecuteLater(20);
        }

        private void OnDisable() => _backdrop?.UnregisterCallback<ClickEvent>(OnBackdropClicked);

        private static void SetText(VisualElement root, string name, string text)
        {
            var label = root.Q<Label>(name);
            if (label != null) label.text = text ?? string.Empty;
        }

        private static void Wire(VisualElement root, string name, System.Action action)
        {
            var button = root.Q<Button>(name);
            if (button != null) button.clicked += action;
        }

        // A row was tapped: remove the menu at once (the screen change that follows replaces the card underneath), then announce.
        private void Choose<T>(T evt) where T : struct
        {
            CloseImmediate();
            EventBus.Publish(evt);
        }

        private void OnBackdropClicked(ClickEvent evt)
        {
            if (_closing || _overlay == null) return;
            _closing = true;
            _panel?.AddToClassList("drawer-panel--closed");
            _backdrop?.AddToClassList("drawer-backdrop--closed");
            _overlay.schedule.Execute(CloseImmediate).ExecuteLater(CloseAnimationMs);
        }

        /// <summary>Removes the menu with no animation. Safe to call twice; also called by the router before any screen swap.</summary>
        public void CloseImmediate()
        {
            _closing = true;
            // uxml.Instantiate() wraps the drawer in a TemplateContainer the router positioned full-screen; removing only
            // the inner element would leave that empty wrapper over the screen, swallowing every tap.
            var host = _overlay?.parent as TemplateContainer;
            (host ?? _overlay)?.RemoveFromHierarchy();
            _overlay = null;
            if (this != null) Destroy(this);
        }
    }
}
