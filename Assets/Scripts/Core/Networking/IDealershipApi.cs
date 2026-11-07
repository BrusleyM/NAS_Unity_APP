using System;

namespace NAS.Core.Networking
{
    public interface IDealershipApi
    {
        void GetDealerships(string accessToken, Action<ApiResult<CustomerDealershipListResponse>> completed);

        /// <summary>Which customer-facing features the dealership has (GET api/customer/dealerships/{id}/features), so the app can hide what is off.</summary>
        void GetFeatures(int dealershipId, string accessToken, Action<ApiResult<DealershipFeaturesResponse>> completed);
    }
}
