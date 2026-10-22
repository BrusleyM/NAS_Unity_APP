using System;

namespace NAS.Core.Networking
{
    [Serializable]
    public sealed class VehicleInteractionTelemetryRequest
    {
        public int customerSessionId;
        public string clientVehicleInteractionId;
        public int vehicleModelId;
        public string categoryName;
        public string startedAt;
        public string endedAt;
        public int zoomInCount;
        public int zoomOutCount;
        public int colourChangeCount;
        public int trimChangeCount;
    }
}
