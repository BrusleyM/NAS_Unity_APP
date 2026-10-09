using System;

namespace NAS.Core.Networking
{
    [Serializable]
    public sealed class SubmitEstimateRequest
    {
        public int vehicleModelId;
        public float depositAmount;
        public float tradeInValue;
        public int termMonths;
        public float interestRate;
        public float estimatedMonthly;
        public float balloonAmount;
        // 0 = no configuration was created (either the customer never went
        // through AR's Confirm flow, or that best-effort call failed) -
        // matches the backend's ">0 means set" convention, since JsonUtility
        // can't send a real null for an unset int.
        public int savedConfigurationId;
        // GameManager.TelemetrySessionId (0 = not started). Lets the backend
        // refuse a vehicle that isn't from the dealership the customer chose.
        public int customerSessionId;
    }
}
