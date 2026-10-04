using System;

namespace NAS.Core.Networking
{
    public interface IDealershipApi
    {
        void GetDealerships(string accessToken, Action<ApiResult<CustomerDealershipListResponse>> completed);
    }
}
