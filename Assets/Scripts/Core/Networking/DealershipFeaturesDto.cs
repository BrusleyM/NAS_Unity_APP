using System;

namespace NAS.Core.Networking
{
    // Mirrors CustomerDealershipFeaturesDto from GET /api/customer/dealerships/{id}/features: only the features a customer sees (ar, finance, test_drive) and
    // whether each works for that dealership.
    [Serializable]
    public sealed class FeatureStateDto
    {
        public string key;
        public bool enabled;
    }

    [Serializable]
    public sealed class DealershipFeaturesResponse
    {
        public int dealershipId;
        public FeatureStateDto[] features;
    }
}
