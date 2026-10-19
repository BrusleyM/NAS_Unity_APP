using System;

namespace NAS.Core.Auth.Dtos
{
    [Serializable]
    public sealed class CustomerLoginRequest
    {
        // Holds the email address OR the username: the field keeps its old name so older servers and apps still agree.
        public string email;
        public string password;
    }
}
