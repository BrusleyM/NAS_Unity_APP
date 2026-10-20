using System;

namespace NAS.Core.Auth.Dtos
{
    /// <summary>POST /api/customer/auth/change-password.</summary>
    [Serializable]
    public sealed class ChangePasswordRequest
    {
        public string currentPassword;
        public string newPassword;
        // This device's refresh token, so it stays signed in while other devices are signed out.
        public string refreshToken;
    }
}
