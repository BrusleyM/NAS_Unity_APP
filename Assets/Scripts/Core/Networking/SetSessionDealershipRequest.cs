using System;

namespace NAS.Core.Networking
{
    [Serializable]
    public sealed class SetSessionDealershipRequest
    {
        // Server-assigned id from POST /api/telemetry/session.
        public int customerSessionId;
        public int dealershipId;
    }
}
