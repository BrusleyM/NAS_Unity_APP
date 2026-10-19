using System;

namespace NAS.Core.Auth.Dtos
{
    /// <summary>
    /// POST /api/customer/auth/resend-verification and /forgot-password. "email" may hold the username instead.
    /// The server answers 200 whether or not such an account exists.
    /// </summary>
    [Serializable]
    public sealed class AccountIdentifierRequest
    {
        public string email;
    }
}
