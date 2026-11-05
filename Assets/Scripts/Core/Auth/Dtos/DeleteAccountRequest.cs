using System;

namespace NAS.Core.Auth.Dtos
{
    /// <summary>POST /api/customer/profile/delete-account. The customer confirms with their password; whose account it is comes from the token.</summary>
    [Serializable]
    public sealed class DeleteAccountRequest
    {
        public string password;
    }
}
