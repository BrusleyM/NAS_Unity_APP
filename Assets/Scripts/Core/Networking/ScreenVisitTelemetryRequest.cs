using System;

namespace NAS.Core.Networking
{
    // One stretch of time on one app screen, sent when the customer leaves it
    // (POST /api/telemetry/screen-visits). Timestamps are ISO 8601 ("o"), same
    // as every other telemetry request.
    [Serializable]
    public sealed class ScreenVisitTelemetryRequest
    {
        // Server-assigned id from POST /api/telemetry/session.
        public int customerSessionId;
        // Unique per visit; the backend treats a retry of the same id as a no-op.
        public string clientScreenVisitId;
        // snake_case screen id, e.g. "car_selection" (see ScreenNames).
        public string screenName;
        public string startedAt;
        public string endedAt;
    }
}
