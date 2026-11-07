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
        void ChangeUsername(ChangeUsernameRequest request, string accessToken, Action<ApiResult<CustomerDto>> completed);
        void ChangePassword(ChangePasswordRequest request, string accessToken, Action<ApiResult<EmptyResponse>> completed);

        /// <summary>A copy of everything held about the customer (GET /api/customer/profile/export), as the server's JSON text.</summary>
        void ExportData(string accessToken, Action<ApiResult<string>> completed);

        /// <summary>"Email me my data" (POST /api/customer/profile/export/email): the same copy, sent as a file to the customer's verified address.</summary>
        void EmailData(string accessToken, Action<ApiResult<EmailedDataCopyResponse>> completed);

        /// <summary>"Delete my account" (POST /api/customer/profile/delete-account). Needs the password; immediate and permanent.</summary>
        void DeleteAccount(DeleteAccountRequest request, string accessToken, Action<ApiResult<DeleteAccountResponse>> completed);
    }
}
