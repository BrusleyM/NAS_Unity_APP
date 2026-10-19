using System.Collections.Generic;
using NAS.Core.Motion;
using UnityEngine;
using UnityEngine.UIElements;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// Small helpers shared by the profile forms (Edit Profile, Change Password), same behaviour as the Login/Register
    /// cards: a red outline plus a message under the field, and a spinner that replaces the button's label while a
    /// request is in flight. Pure UI Toolkit, no game state.
    /// </summary>
    internal static class FormHelpers
    {
        // A message appearing under a field rises into place and the field shakes; clearing it just hides it.
        public static void SetFieldError(TextField field, Label errorLabel, string message)
        {
            var hasError = !string.IsNullOrEmpty(message);
            if (field != null) field.EnableInClassList("input-field--error", hasError);
            if (hasError) UiMotion.Shake(field);
            Show(errorLabel, message);
        }

        public static void SetMessage(Label label, string message) => Show(label, message);

        private static void Show(Label label, string message)
        {
            if (label == null) return;
            var hasMessage = !string.IsNullOrEmpty(message);
            label.text = message ?? string.Empty;
            label.style.display = hasMessage ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasMessage) UiMotion.Enter(label, 0f, 6f, 0.22f);
        }

        public static void SetSubmitting(Button button, Label buttonLabel, VisualElement spinner, bool submitting)
        {
            if (button != null) button.SetEnabled(!submitting);
            if (buttonLabel != null) buttonLabel.style.display = submitting ? DisplayStyle.None : DisplayStyle.Flex;
            if (spinner != null) spinner.style.display = submitting ? DisplayStyle.Flex : DisplayStyle.None;
            if (submitting) UiMotion.Spin(spinner);
            else UiMotion.StopSpin(spinner);
        }

        public static string ErrorFor(IDictionary<string, string> errors, string key) =>
            errors != null && errors.TryGetValue(key, out var message) ? message : null;
    }
}
