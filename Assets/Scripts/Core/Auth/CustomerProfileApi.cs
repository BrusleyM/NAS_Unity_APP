using System;
using NAS.Core.Auth.Dtos;
using NAS.Core.Networking;
using UnityEngine;

namespace NAS.Core.Auth
{
    public sealed class CustomerProfileApi : ICustomerProfileApi
    {
        private readonly MonoBehaviour _coroutineRunner;
        private readonly ApiClient _client;

        public CustomerProfileApi(MonoBehaviour coroutineRunner, ApiSettings settings, bool trustAnyCertificate = false)
        {
            _coroutineRunner = coroutineRunner;
            _client = new ApiClient(settings, trustAnyCertificate);
        }

        public void GetProfile(string accessToken, Action<ApiResult<CustomerDto>> completed) =>
            _coroutineRunner.StartCoroutine(_client.GetJson("api/customer/profile", completed, accessToken));

        public void UpdateProfile(UpdateProfileRequest request, string accessToken, Action<ApiResult<CustomerDto>> completed) =>
            _coroutineRunner.StartCoroutine(_client.PutJson("api/customer/profile", request, completed, accessToken));

        public void ChangePassword(ChangePasswordRequest request, string accessToken, Action<ApiResult<EmptyResponse>> completed) =>
            _coroutineRunner.StartCoroutine(_client.PostJson("api/customer/auth/change-password", request, completed, accessToken));
    }
}
