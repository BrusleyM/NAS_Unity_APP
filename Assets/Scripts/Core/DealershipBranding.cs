using System;

namespace NAS.Core
{
    // What the app takes from a dealership's Settings: where the estimator starts and where its logo is.
    public static class DealershipBranding
    {
        public const float FallbackInterestRate = 4.5f;
        private const float MaxInterestRate = 15f; // the estimator's slider ends here

        // 0 means the dealership never set one (the app cannot receive null), so the estimator keeps its own start.
        public static float StartingInterestRate(float configured) =>
            configured > 0f && configured <= MaxInterestRate ? configured : FallbackInterestRate;

        // Uploaded images come back as "/uploads/..." paths; a logo pasted into Settings is usually a full address.
        public static string ImageUrl(string url, string apiBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            url = url.Trim();
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return url;
            return string.IsNullOrEmpty(apiBaseUrl) ? url : $"{apiBaseUrl}{url}";
        }
    }
}
