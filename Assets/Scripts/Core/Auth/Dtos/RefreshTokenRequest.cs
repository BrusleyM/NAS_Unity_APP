using System;

namespace NAS.Core.Auth.Dtos
{
    [Serializable]
    public sealed class RefreshTokenRequest
    {
        public string refreshToken;
    }
}
