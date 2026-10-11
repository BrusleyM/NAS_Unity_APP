using System;

namespace NAS.Core.Auth.Dtos
{
    /// <summary>PUT /api/customer/profile. The email is not here on purpose: it is the sign-in identity and is read-only.</summary>
    [Serializable]
    public sealed class UpdateProfileRequest
    {
        public string firstName;
        public string lastName;
        public string cellNumber;
    }
}
