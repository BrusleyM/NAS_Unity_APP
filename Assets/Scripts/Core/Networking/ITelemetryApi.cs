using System;

namespace NAS.Core.Networking
{
    public interface ITelemetryApi
    {
        void StartSession(CustomerSessionTelemetryRequest request, string accessToken, Action<ApiResult<TelemetryAckResponse>> completed);
        void SetSessionDealership(SetSessionDealershipRequest request, string accessToken, Action<ApiResult<TelemetryAckResponse>> completed);
        void EndSession(EndCustomerSessionRequest request, string accessToken, Action<ApiResult<TelemetryAckResponse>> completed);
        void LogEvent(ActivityEventTelemetryRequest request, string accessToken, Action<ApiResult<TelemetryAckResponse>> completed);
        void LogVehicleInteraction(VehicleInteractionTelemetryRequest request, string accessToken, Action<ApiResult<TelemetryAckResponse>> completed);
        void LogArSession(ArSessionTelemetryRequest request, string accessToken, Action<ApiResult<TelemetryAckResponse>> completed);
        void LogAffordabilitySession(AffordabilitySessionTelemetryRequest request, string accessToken, Action<ApiResult<TelemetryAckResponse>> completed);
    }
}
