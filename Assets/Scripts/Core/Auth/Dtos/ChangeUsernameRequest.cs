using System;

namespace NAS.Core.Auth.Dtos
{
    /// <summary>PUT /api/customer/profile/username. The generated username can be replaced once.</summary>
    [Serializable]
    public sealed class ChangeUsernameRequest
    {
        public string username;
    }
}
