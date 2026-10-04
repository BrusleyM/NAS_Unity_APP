using System;
using UnityEngine;

namespace NAS.Core.Networking
{
    public sealed class DealershipApi : IDealershipApi
    {
        private readonly MonoBehaviour _coroutineRunner;
        private readonly ApiClient _client;

        public DealershipApi(MonoBehaviour coroutineRunner, ApiSettings settings, bool trustAnyCertificate = false)
        {
            _coroutineRunner = coroutineRunner;
            _client = new ApiClient(settings, trustAnyCertificate);
        }

        public void GetDealerships(string accessToken, Action<ApiResult<CustomerDealershipListResponse>> completed)
        {
            _coroutineRunner.StartCoroutine(_client.GetJson<CustomerDealershipListResponse>(
                "api/customer/dealerships", completed, accessToken));
        }
    }
}
