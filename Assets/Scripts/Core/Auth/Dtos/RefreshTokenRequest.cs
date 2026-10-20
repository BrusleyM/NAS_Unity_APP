using System;

namespace NAS.Core.Auth.Dtos
{
    /// <summary>POST /api/customer/auth/refresh and /logout.</summary>
    [Serializable]
    public sealed class RefreshTokenRequest
    {
        public string refreshToken;
    }
}
