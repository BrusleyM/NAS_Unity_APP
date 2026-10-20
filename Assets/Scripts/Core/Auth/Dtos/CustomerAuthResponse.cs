using System;

namespace NAS.Core.Auth.Dtos
{
    [Serializable]
    public sealed class CustomerAuthResponse
    {
        public string accessToken;
        public string expiresAtUtc;
        public CustomerDto customer;
        // Swapped at /refresh for a new sign-in; each one works once. Empty when the server issued no sign-in.
        public string refreshToken;
        public string refreshExpiresAtUtc;
        // True only from register when the server wants the email verified first: there is then no token (accessToken is empty).
        public bool emailVerificationRequired;
    }
}
