using System;

namespace NAS.Core.Networking
{
    // 0 means not chosen (JsonUtility cannot send null): the backend uses the vehicle's default.
    [Serializable]
    public sealed class CreateConfigurationRequest
    {
        public int vehicleModelId;

        public int exteriorColorOptionId;
        public int wheelOptionId;
        public int interiorOptionId;
        public int trimLevelId;
    }
}
