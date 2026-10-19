using System;
using NAS.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// What the screens that take an emailed code share: the six-digit box (digits only, numeric keypad) and the
    /// "Resend code" button with its 60-second countdown. UI plumbing only; the rules are in AccountRecovery/ProfileValidation.
    /// </summary>
    internal static class CodeCardSupport
    {
        /// <summary>Makes a text field a six-digit code box: numeric keypad, and anything typed or pasted is cut down to its first six digits.</summary>
        public static void ConfigureCodeField(TextField field)
        {
            if (field == null) return;
            field.maxLength = ProfileValidation.CodeLength;
            field.keyboardType = TouchScreenKeyboardType.NumberPad;
            field.RegisterValueChangedCallback(evt =>
            {
                var cleaned = ProfileValidation.NormalizeCode(evt.newValue);
                if (cleaned != evt.newValue) field.SetValueWithoutNotify(cleaned);
            });
        }

        /// <summary>
        /// Drives a "Resend code" button: disabled with a countdown for 60 seconds after a code was sent, then ready.
        /// Restart() is called whenever a code goes out.
        /// </summary>
        public sealed class ResendButton
        {
            private readonly Button _button;
            private IVisualElementScheduledItem _ticker;
            private DateTime _sentUtc;

            public ResendButton(Button button) => _button = button;

            public void Restart()
            {
                _sentUtc = DateTime.UtcNow;
                if (_button == null) return;
                Refresh();
                _ticker ??= _button.schedule.Execute(Refresh).Every(500);
                _ticker.Resume();
            }

            public void Stop() => _ticker?.Pause();

            private void Refresh()
            {
                if (_button == null) return;
                var left = AccountRecovery.SecondsLeft(_sentUtc, DateTime.UtcNow);
                _button.text = AccountRecovery.ResendLabel(left);
                _button.SetEnabled(left == 0);
                if (left == 0) _ticker?.Pause();
            }
        }
    }
}
