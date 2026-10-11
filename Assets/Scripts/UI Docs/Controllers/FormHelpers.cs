using System.Collections.Generic;
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
        public const float SpinDegreesPerSecond = 220f; // same speed as LoginCardController / LoadingOverlayController

        public static void SetFieldError(TextField field, Label errorLabel, string message)
        {
            var hasError = !string.IsNullOrEmpty(message);
            if (field != null) field.EnableInClassList("input-field--error", hasError);
            if (errorLabel == null) return;
            errorLabel.text = message ?? string.Empty;
            errorLabel.style.display = hasError ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static void SetMessage(Label label, string message)
        {
            if (label == null) return;
            label.text = message ?? string.Empty;
            label.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public static void SetSubmitting(Button button, Label buttonLabel, VisualElement spinner, bool submitting)
        {
            if (button != null) button.SetEnabled(!submitting);
            if (buttonLabel != null) buttonLabel.style.display = submitting ? DisplayStyle.None : DisplayStyle.Flex;
            if (spinner != null) spinner.style.display = submitting ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static float Spin(VisualElement spinner, float angle, float deltaTime)
        {
            if (spinner == null) return angle;
            angle = (angle + SpinDegreesPerSecond * deltaTime) % 360f;
            spinner.style.rotate = new StyleRotate(new Rotate(new Angle(angle, AngleUnit.Degree)));
            return angle;
        }

        public static string ErrorFor(IDictionary<string, string> errors, string key) =>
            errors != null && errors.TryGetValue(key, out var message) ? message : null;
    }
}
