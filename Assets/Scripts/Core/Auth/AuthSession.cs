using System;
using NAS.Core.Auth.Dtos;
using NAS.Core.Models;

namespace NAS.Core.Auth
{
    public sealed class AuthSession
    {
        private readonly TokenStorage _tokenStorage;

        public AuthSession(TokenStorage tokenStorage)
        {
            _tokenStorage = tokenStorage;
        }

        public User CurrentUser { get; private set; }
        public string AccessToken => _tokenStorage.AccessToken;
        public string ExpiresAtUtc => _tokenStorage.ExpiresAtUtc;
        public string RefreshToken => _tokenStorage.RefreshToken;
        public string RefreshExpiresAtUtc => _tokenStorage.RefreshExpiresAtUtc;
        public bool IsAuthenticated => CurrentUser != null && !string.IsNullOrWhiteSpace(AccessToken);

        public void Apply(NAS.Core.Auth.Dtos.CustomerAuthResponse response)
        {
            if (response?.customer == null || string.IsNullOrWhiteSpace(response.accessToken))
                throw new ArgumentException("The authentication response was incomplete.");

            CurrentUser = new User
            {
                id = response.customer.id.ToString(),
                email = response.customer.email,
                displayName = $"{response.customer.firstName} {response.customer.lastName}".Trim()
            };
            _tokenStorage.Save(response.accessToken, response.expiresAtUtc, response.refreshToken, response.refreshExpiresAtUtc);
        }

        public void Logout()
        {
            CurrentUser = null;
            _tokenStorage.Clear();
        }
    }
}
