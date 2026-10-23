using System;

namespace NAS.Core.Auth.Dtos
{
    [Serializable]
    public sealed class TermsResponse
    {
        public string version;
        public string title;
        public TermsSection[] sections;
    }

    [Serializable]
    public sealed class TermsSection
    {
        public string heading;
        public string body;
    }
}
