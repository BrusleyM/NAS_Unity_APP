using System;

namespace NAS.Core.Vehicles.Dtos
{
    [Serializable]
    public sealed class VehicleDto
    {
        public int id;
        public string name;
        public int year;
        public float basePrice;
        public string bodyType;
        public string powertrain;
        public string imageUrl;
        public string tigrisModelKey;
        public VehicleColorDto[] exteriorColors;
        public VehicleColorDto[] wheels;
        public VehicleColorDto[] interiors;
        public VehicleColorDto[] trimLevels;
    }
}
