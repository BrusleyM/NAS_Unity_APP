using System;
using NAS.Core.Auth.Dtos;
using NAS.Core.Networking;

namespace NAS.Core.Auth
{
    /// <summary>The signed-in customer's own profile (the app's menu). Every call needs the access token.</summary>
    public interface ICustomerProfileApi
    {
        void GetProfile(string accessToken, Action<ApiResult<CustomerDto>> completed);
        void UpdateProfile(UpdateProfileRequest request, string accessToken, Action<ApiResult<CustomerDto>> completed);
        void ChangePassword(ChangePasswordRequest request, string accessToken, Action<ApiResult<EmptyResponse>> completed);
    }
}
