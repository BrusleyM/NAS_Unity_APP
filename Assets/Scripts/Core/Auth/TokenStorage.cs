using UnityEngine;

namespace NAS.Core.Auth
{
    // PlayerPrefs is plain text on the device; secure storage comes later.
    public sealed class TokenStorage
    {
        private const string AccessTokenKey = "nas.customer.access_token";
        private const string ExpiryKey = "nas.customer.expires_at_utc";
        private const string RefreshTokenKey = "nas.customer.refresh_token";
        private const string RefreshExpiryKey = "nas.customer.refresh_expires_at_utc";

        public TokenStorage()
        {
            AccessToken = PlayerPrefs.GetString(AccessTokenKey, null);
            ExpiresAtUtc = PlayerPrefs.GetString(ExpiryKey, null);
            RefreshToken = PlayerPrefs.GetString(RefreshTokenKey, null);
            RefreshExpiresAtUtc = PlayerPrefs.GetString(RefreshExpiryKey, null);
        }

        public string AccessToken { get; private set; }
        public string ExpiresAtUtc { get; private set; }
        public string RefreshToken { get; private set; }
        public string RefreshExpiresAtUtc { get; private set; }

        public void Save(string accessToken, string expiresAtUtc, string refreshToken, string refreshExpiresAtUtc)
        {
            AccessToken = accessToken;
            ExpiresAtUtc = expiresAtUtc;
            RefreshToken = refreshToken;
            RefreshExpiresAtUtc = refreshExpiresAtUtc;

            PlayerPrefs.SetString(AccessTokenKey, accessToken ?? string.Empty);
            PlayerPrefs.SetString(ExpiryKey, expiresAtUtc ?? string.Empty);
            PlayerPrefs.SetString(RefreshTokenKey, refreshToken ?? string.Empty);
            PlayerPrefs.SetString(RefreshExpiryKey, refreshExpiresAtUtc ?? string.Empty);
            PlayerPrefs.Save();
        }

        public void Clear()
        {
            AccessToken = ExpiresAtUtc = RefreshToken = RefreshExpiresAtUtc = null;
            PlayerPrefs.DeleteKey(AccessTokenKey);
            PlayerPrefs.DeleteKey(ExpiryKey);
            PlayerPrefs.DeleteKey(RefreshTokenKey);
            PlayerPrefs.DeleteKey(RefreshExpiryKey);
            PlayerPrefs.Save();
        }
    }
}
