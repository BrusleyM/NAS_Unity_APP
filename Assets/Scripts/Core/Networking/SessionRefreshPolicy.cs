using System;
using System.Globalization;

namespace NAS.Core.Networking
{
    public static class SessionRefreshPolicy
    {
        public const int LeadMinutes = 5;
        public const float CheckSeconds = 15f;
        public const float RetrySeconds = 30f;

        public static bool NeedsRefresh(string accessExpiresAtUtc, DateTime nowUtc)
        {
            if (!TryParse(accessExpiresAtUtc, out var expires)) return true;
            return expires - nowUtc <= TimeSpan.FromMinutes(LeadMinutes);
        }

        public static bool CanRestore(string refreshToken, string refreshExpiresAtUtc, DateTime nowUtc)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return false;
            return !TryParse(refreshExpiresAtUtc, out var expires) || expires > nowUtc;
        }

        // Only a 401 ends the login; no connection or a server error keeps the saved tokens.
        public static bool EndsSession(long statusCode) => statusCode == 401;

        private static bool TryParse(string value, out DateTime utc) =>
            DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);
    }
}
