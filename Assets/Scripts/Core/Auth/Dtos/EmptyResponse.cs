using System;

namespace NAS.Core.Auth.Dtos
{
    /// <summary>The API answers some calls with an empty JSON object ("{}"); ApiClient always parses a body.</summary>
    [Serializable]
    public sealed class EmptyResponse
    {
    }
}
