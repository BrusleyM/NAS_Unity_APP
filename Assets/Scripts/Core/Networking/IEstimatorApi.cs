using System;

namespace NAS.Core.Networking
{
    public interface IEstimatorApi
    {
        void SubmitEstimate(SubmitEstimateRequest request, string accessToken, Action<ApiResult<SubmitEstimateResponse>> completed);
        void RequestTestDrive(RequestTestDriveRequest request, string accessToken, Action<ApiResult<RequestTestDriveResponse>> completed);
    }
}
