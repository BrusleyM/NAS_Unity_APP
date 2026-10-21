using System.Collections.Generic;

namespace NAS.Core.Models
{
    [System.Serializable]
    public class VehicleInfo
    {
        public int id;
        public string modelName;
        public float retailPrice;
        public string tigrisModelKey;
        public List<CarColorOption> exteriorColors = new List<CarColorOption>();
        public List<CarColorOption> wheels = new List<CarColorOption>();
        public List<CarColorOption> interiors = new List<CarColorOption>();
        public List<CarColorOption> trimLevels = new List<CarColorOption>();
    }
}