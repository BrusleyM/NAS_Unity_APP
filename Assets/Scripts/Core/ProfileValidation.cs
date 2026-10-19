using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NAS.Core
{
    /// <summary>
    /// Client-side checks for the profile screens, run before any request is sent. The rules are the ones
    /// registration already uses (AuthController) and what the backend enforces, so a value that passes here is
    /// not rejected by the server for a different reason. Pure logic (no Unity, no UI) so it is unit-tested.
    /// Results are keyed by field name, like AuthFailedEvent.FieldErrors.
    /// </summary>
    public static class ProfileValidation
    {
        public const int MinPasswordLength = 8;
        public const int MaxPasswordLength = 128;

        public const int CodeLength = 6;

        private static readonly Regex CellNumberRegex = new Regex(@"^\+?[\d\s\-()]{7,15}$", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);
        // Same shape the server enforces: starts with a letter, then letters, digits, dot or underscore; 3 to 30 characters.
        private static readonly Regex UsernameRegex = new Regex(@"^[a-z][a-z0-9._]{2,29}$", RegexOptions.Compiled);

        public static Dictionary<string, string> ValidateProfile(string firstName, string lastName, string cellNumber)
        {
            var errors = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(firstName))
                errors["firstName"] = "First name is required.";
            else if (firstName.Trim().Length > 100)
                errors["firstName"] = "First name is too long.";

            if (string.IsNullOrWhiteSpace(lastName))
                errors["lastName"] = "Last name is required.";
            else if (lastName.Trim().Length > 100)
                errors["lastName"] = "Last name is too long.";

            if (string.IsNullOrWhiteSpace(cellNumber))
                errors["cellNumber"] = "Cell number is required.";
            else if (!CellNumberRegex.IsMatch(cellNumber.Trim()))
                errors["cellNumber"] = "Enter a valid cell number.";

            return errors;
        }

        public static Dictionary<string, string> ValidatePasswordChange(string currentPassword, string newPassword, string confirmPassword)
        {
            var errors = new Dictionary<string, string>();

            if (string.IsNullOrEmpty(currentPassword))
                errors["currentPassword"] = "Enter your current password.";

            if (string.IsNullOrEmpty(newPassword))
                errors["newPassword"] = "Enter a new password.";
            else if (newPassword.Length < MinPasswordLength)
                errors["newPassword"] = $"Password must be at least {MinPasswordLength} characters.";
            else if (newPassword.Length > MaxPasswordLength)
                errors["newPassword"] = "Password is too long.";
            else if (newPassword == currentPassword)
                errors["newPassword"] = "Choose a password that is different from your current one.";

            if (string.IsNullOrEmpty(confirmPassword))
                errors["confirmPassword"] = "Please confirm your new password.";
            else if (confirmPassword != newPassword)
                errors["confirmPassword"] = "Passwords do not match.";

            return errors;
        }

        /// <summary>
        /// The sign-in box takes an email address or a username (an "@" means email). Returns what is wrong with it, or
        /// null when it is acceptable. The server decides whether the account exists.
        /// </summary>
        public static string ValidateLoginIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return "Enter your email or username.";

            var typed = identifier.Trim();
            if (typed.Contains("@"))
                return EmailRegex.IsMatch(typed) ? null : "Enter a valid email address.";

            return UsernameRegex.IsMatch(typed.ToLowerInvariant()) ? null : "Enter a valid username or your email address.";
        }

        public static string NormalizeUsername(string username) => (username ?? string.Empty).Trim().ToLowerInvariant();

        /// <summary>The rule the server applies to a chosen username. Returns what is wrong, or null.</summary>
        public static string ValidateUsername(string username)
        {
            var normalized = NormalizeUsername(username);
            if (normalized.Length == 0)
                return "Enter a username.";
            return UsernameRegex.IsMatch(normalized)
                ? null
                : "Use 3 to 30 characters: start with a letter, then letters, numbers, dots or underscores.";
        }

        /// <summary>Keeps only the digits of whatever was typed or pasted into a code box, at most six.</summary>
        public static string NormalizeCode(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var digits = new System.Text.StringBuilder(CodeLength);
            foreach (var ch in raw)
            {
                if (ch >= '0' && ch <= '9') digits.Append(ch);
                if (digits.Length == CodeLength) break;
            }
            return digits.ToString();
        }

        /// <summary>Null when the box holds a full six-digit code.</summary>
        public static string ValidateCode(string code) =>
            NormalizeCode(code).Length == CodeLength ? null : $"Enter the {CodeLength}-digit code from the email.";

        public static Dictionary<string, string> ValidatePasswordReset(string code, string newPassword, string confirmPassword)
        {
            var errors = new Dictionary<string, string>();

            var codeError = ValidateCode(code);
            if (codeError != null)
                errors["code"] = codeError;

            if (string.IsNullOrEmpty(newPassword))
                errors["newPassword"] = "Enter a new password.";
            else if (newPassword.Length < MinPasswordLength)
                errors["newPassword"] = $"Password must be at least {MinPasswordLength} characters.";
            else if (newPassword.Length > MaxPasswordLength)
                errors["newPassword"] = "Password is too long.";

            if (string.IsNullOrEmpty(confirmPassword))
                errors["confirmPassword"] = "Please confirm your new password.";
            else if (confirmPassword != newPassword)
                errors["confirmPassword"] = "Passwords do not match.";

            return errors;
        }

        /// <summary>Up to two capital letters from a display name ("Ada Lovelace" -> "AL"); "?" when there is no name.</summary>
        public static string Initials(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                return "?";

            var parts = displayName.Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return parts[0].Substring(0, 1).ToUpperInvariant();

            return (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpperInvariant();
        }
    }
}
