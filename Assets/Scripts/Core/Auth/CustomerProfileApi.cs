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

        public void ChangeUsername(ChangeUsernameRequest request, string accessToken, Action<ApiResult<CustomerDto>> completed) =>
            _coroutineRunner.StartCoroutine(_client.PutJson("api/customer/profile/username", request, completed, accessToken));

        public void ChangePassword(ChangePasswordRequest request, string accessToken, Action<ApiResult<EmptyResponse>> completed) =>
            _coroutineRunner.StartCoroutine(_client.PostJson("api/customer/auth/change-password", request, completed, accessToken));

        public void ExportData(string accessToken, Action<ApiResult<string>> completed) =>
            _coroutineRunner.StartCoroutine(_client.GetText("api/customer/profile/export", completed, accessToken));

        public void EmailData(string accessToken, Action<ApiResult<EmailedDataCopyResponse>> completed) =>
            _coroutineRunner.StartCoroutine(_client.PostJson("api/customer/profile/export/email", new EmptyResponse(), completed, accessToken));

        public void DeleteAccount(DeleteAccountRequest request, string accessToken, Action<ApiResult<DeleteAccountResponse>> completed) =>
            _coroutineRunner.StartCoroutine(_client.PostJson("api/customer/profile/delete-account", request, completed, accessToken));
    }
}
