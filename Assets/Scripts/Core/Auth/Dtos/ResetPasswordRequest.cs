using System;

namespace NAS.Core.Auth.Dtos
{
    /// <summary>POST /api/customer/auth/reset-password. "email" may hold the username instead.</summary>
    [Serializable]
    public sealed class ResetPasswordRequest
    {
        public string email;
        public string code;
        public string newPassword;
    }
}
