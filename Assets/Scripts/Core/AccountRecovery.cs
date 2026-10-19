using System;

namespace NAS.Core
{
    /// <summary>
    /// Small pure helpers for the screens that deal with emailed codes (verify email, forgot password).
    /// No Unity, no UI: unit-tested.
    /// </summary>
    public static class AccountRecovery
    {
        /// <summary>How long "Resend code" stays disabled after a code was sent. The server ignores a second request inside this window anyway.</summary>
        public const int ResendCooldownSeconds = 60;

        /// <summary>Whole seconds still to wait (0 = can resend now). Never below 0 or above the cooldown, even if the clock moved.</summary>
        public static int SecondsLeft(DateTime sentUtc, DateTime nowUtc, int cooldownSeconds = ResendCooldownSeconds)
        {
            var left = (int)Math.Ceiling(cooldownSeconds - (nowUtc - sentUtc).TotalSeconds);
            if (left < 0) return 0;
            return left > cooldownSeconds ? cooldownSeconds : left;
        }

        public static string ResendLabel(int secondsLeft) =>
            secondsLeft > 0 ? $"Resend code in {secondsLeft}s" : "Resend code";

        /// <summary>"ada@example.com" -> "a**@example.com": enough to recognise it without printing it in full on screen.</summary>
        public static string MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return string.Empty;
            var trimmed = email.Trim();
            var at = trimmed.IndexOf('@');
            if (at <= 0) return trimmed;
            var local = trimmed.Substring(0, at);
            var stars = new string('*', Math.Max(2, Math.Min(local.Length - 1, 6)));
            return local.Substring(0, 1) + stars + trimmed.Substring(at);
        }

        /// <summary>True when what the person typed is an email address rather than a username.</summary>
        public static bool LooksLikeEmail(string identifier) => !string.IsNullOrEmpty(identifier) && identifier.Contains("@");
    }
}
