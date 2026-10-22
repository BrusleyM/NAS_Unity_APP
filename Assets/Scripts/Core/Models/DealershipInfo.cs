namespace NAS.Core.Models
{
    /// <summary>The dealership the customer chose to shop from this session.</summary>
    [System.Serializable]
    public class DealershipInfo
    {
        public int id;
        public string name;
        public string logoUrl;
        public string primaryColor;
        public float interestRate;
    }
}
