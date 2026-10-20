using System;
using NAS.Core.Auth.Dtos;
using NAS.Core.Networking;

namespace NAS.Core.Auth
{
    public interface ICustomerAuthApi
    {
        void Login(CustomerLoginRequest request, Action<ApiResult<CustomerAuthResponse>> completed);
        void Register(CustomerRegisterRequest request, Action<ApiResult<CustomerAuthResponse>> completed);
        /// <summary>The emailed code proves the address and signs the customer in (the answer is a normal login response).</summary>
        void VerifyEmail(VerifyEmailRequest request, Action<ApiResult<CustomerAuthResponse>> completed);
        void ResendVerification(AccountIdentifierRequest request, Action<ApiResult<EmptyResponse>> completed);
        void ForgotPassword(AccountIdentifierRequest request, Action<ApiResult<EmptyResponse>> completed);
        void Refresh(RefreshTokenRequest request, Action<ApiResult<CustomerAuthResponse>> completed);
        void Logout(RefreshTokenRequest request, Action<ApiResult<EmptyResponse>> completed);
        void ResetPassword(ResetPasswordRequest request, Action<ApiResult<EmptyResponse>> completed);
    }
}
