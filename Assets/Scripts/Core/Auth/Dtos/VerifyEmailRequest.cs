using System;

namespace NAS.Core.Auth.Dtos
{
    /// <summary>POST /api/customer/auth/verify-email. "email" may hold the username instead.</summary>
    [Serializable]
    public sealed class VerifyEmailRequest
    {
        public string email;
        public string code;
    }
}
