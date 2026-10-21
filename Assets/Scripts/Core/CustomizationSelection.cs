namespace NAS.Core
{
    // Each part's chosen option id (0 = not chosen) and what it adds to the price.
    public sealed class CustomizationSelection
    {
        public int ExteriorColorOptionId { get; private set; }
        public int WheelOptionId { get; private set; }
        public int InteriorOptionId { get; private set; }
        public int TrimLevelId { get; private set; }

        private float _colourPrice;
        private float _wheelPrice;
        private float _interiorPrice;
        private float _trimPrice;

        public float OptionsTotal => _colourPrice + _wheelPrice + _interiorPrice + _trimPrice;

        public void SetColour(int id, float price) { ExteriorColorOptionId = id; _colourPrice = price; }
        public void SetWheel(int id, float price) { WheelOptionId = id; _wheelPrice = price; }
        public void SetInterior(int id, float price) { InteriorOptionId = id; _interiorPrice = price; }
        public void SetTrim(int id, float price) { TrimLevelId = id; _trimPrice = price; }

        public void Clear()
        {
            ExteriorColorOptionId = WheelOptionId = InteriorOptionId = TrimLevelId = 0;
            _colourPrice = _wheelPrice = _interiorPrice = _trimPrice = 0f;
        }
    }
}
