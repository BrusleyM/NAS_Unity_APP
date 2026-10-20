using System;
using System.Globalization;

namespace NAS.Core.Networking
{
    // When to swap the saved refresh token for a new access token, and what a failed swap means. Pure, so it is unit-tested.
    public static class SessionRefreshPolicy
    {
        public const int LeadMinutes = 5;
        public const float CheckSeconds = 15f;
        public const float RetrySeconds = 30f;

        // True when the access token is unreadable or lapses within LeadMinutes.
        public static bool NeedsRefresh(string accessExpiresAtUtc, DateTime nowUtc)
        {
            if (!TryParse(accessExpiresAtUtc, out var expires)) return true;
            return expires - nowUtc <= TimeSpan.FromMinutes(LeadMinutes);
        }

        // A saved refresh token worth trying at launch: present and not already past its own expiry.
        public static bool CanRestore(string refreshToken, string refreshExpiresAtUtc, DateTime nowUtc)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return false;
            return !TryParse(refreshExpiresAtUtc, out var expires) || expires > nowUtc;
        }

        // Only the server saying "no" ends the login. No connection or a server error keeps the saved tokens for the next try.
        public static bool EndsSession(long statusCode) => statusCode == 401;

        private static bool TryParse(string value, out DateTime utc) =>
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);
    }
}
