using System;

namespace NAS.Core.Auth.Dtos
{
    [Serializable]
    public sealed class CustomerDto
    {
        public int id;
        public string email;
        public string firstName;
        public string lastName;
        public string cellNumber;
        public string status;
        public bool emailVerified;
        // The sign-in name (ada4821); blank from a server that predates usernames.
        public string username;
        // True until the generated username has been replaced once.
        public bool canChangeUsername;
        public string createdAt;
    }
}
