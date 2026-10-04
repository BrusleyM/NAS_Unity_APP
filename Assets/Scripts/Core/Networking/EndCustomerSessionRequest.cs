using System;

namespace NAS.Core.Networking
{
    [Serializable]
    public sealed class EndCustomerSessionRequest
    {
        public int customerSessionId;
        // ISO 8601 ("o" format) - same convention as every other telemetry
        // timestamp in this project (see CustomerSessionTelemetryRequest).
        public string endedAt;
    }
}
