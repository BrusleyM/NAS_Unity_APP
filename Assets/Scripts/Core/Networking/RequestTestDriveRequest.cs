using System;

namespace NAS.Core.Networking
{
    [Serializable]
    public sealed class RequestTestDriveRequest
    {
        public int vehicleModelId;
        // Server-assigned id from POST /api/telemetry/session
        // (GameManager.TelemetrySessionId).
        public int customerSessionId;
        // Unique per tap; the backend treats a repeat of the same id as a no-op.
        public string clientEventId;
        // 0 = no configuration saved yet (backend's ">0 means set" convention).
        public int savedConfigurationId;
    }

    [Serializable]
    public sealed class RequestTestDriveResponse
    {
        public int leadId;
        public bool alreadyRequested;
    }
}
