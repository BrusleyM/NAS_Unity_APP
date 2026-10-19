using System;
using NAS.Core.Auth.Dtos;
using NAS.Core.Networking;
using UnityEngine;

namespace NAS.Core.Auth
{
    public sealed class CustomerAuthApi : ICustomerAuthApi
    {
        private readonly MonoBehaviour _coroutineRunner;
        private readonly ApiClient _client;

        public CustomerAuthApi(MonoBehaviour coroutineRunner, ApiSettings settings, bool trustAnyCertificate = false)
        {
            _coroutineRunner = coroutineRunner;
            _client = new ApiClient(settings, trustAnyCertificate);
        }

        public void Login(CustomerLoginRequest request, Action<ApiResult<CustomerAuthResponse>> completed)
        {
            _coroutineRunner.StartCoroutine(_client.PostJson("api/customer/auth/login", request, completed));
        }

        public void Register(CustomerRegisterRequest request, Action<ApiResult<CustomerAuthResponse>> completed)
        {
            _coroutineRunner.StartCoroutine(_client.PostJson("api/customer/auth/register", request, completed));
        }

        public void VerifyEmail(VerifyEmailRequest request, Action<ApiResult<CustomerAuthResponse>> completed)
        {
            _coroutineRunner.StartCoroutine(_client.PostJson("api/customer/auth/verify-email", request, completed));
        }

        public void ResendVerification(AccountIdentifierRequest request, Action<ApiResult<EmptyResponse>> completed)
        {
            _coroutineRunner.StartCoroutine(_client.PostJson("api/customer/auth/resend-verification", request, completed));
        }

        public void ForgotPassword(AccountIdentifierRequest request, Action<ApiResult<EmptyResponse>> completed)
        {
            _coroutineRunner.StartCoroutine(_client.PostJson("api/customer/auth/forgot-password", request, completed));
        }

        public void ResetPassword(ResetPasswordRequest request, Action<ApiResult<EmptyResponse>> completed)
        {
            _coroutineRunner.StartCoroutine(_client.PostJson("api/customer/auth/reset-password", request, completed));
        }
    }
}
