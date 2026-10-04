using System;
using UnityEngine;

namespace NAS.Core.Networking
{
    public sealed class TelemetryApi : ITelemetryApi
    {
        private readonly MonoBehaviour _coroutineRunner;
        private readonly ApiClient _client;

        public TelemetryApi(MonoBehaviour coroutineRunner, ApiSettings settings, bool trustAnyCertificate = false)
        {
            _coroutineRunner = coroutineRunner;
            _client = new ApiClient(settings, trustAnyCertificate);
        }

        public void StartSession(CustomerSessionTelemetryRequest request, string accessToken, Action<ApiResult<TelemetryAckResponse>> completed) =>
            _coroutineRunner.StartCoroutine(_client.PostJson<CustomerSessionTelemetryRequest, TelemetryAckResponse>(
                "api/telemetry/session", request, completed, accessToken));

        public void EndSession(EndCustomerSessionRequest request, string accessToken, Action<ApiResult<TelemetryAckResponse>> completed) =>
            _coroutineRunner.StartCoroutine(_client.PostJson<EndCustomerSessionRequest, TelemetryAckResponse>(
                "api/telemetry/session/end", request, completed, accessToken));

        public void LogEvent(ActivityEventTelemetryRequest request, string accessToken, Action<ApiResult<TelemetryAckResponse>> completed) =>
            _coroutineRunner.StartCoroutine(_client.PostJson<ActivityEventTelemetryRequest, TelemetryAckResponse>(
                "api/telemetry/events", request, completed, accessToken));

        public void LogVehicleInteraction(VehicleInteractionTelemetryRequest request, string accessToken, Action<ApiResult<TelemetryAckResponse>> completed) =>
            _coroutineRunner.StartCoroutine(_client.PostJson<VehicleInteractionTelemetryRequest, TelemetryAckResponse>(
                "api/telemetry/vehicle-interactions", request, completed, accessToken));

        public void LogArSession(ArSessionTelemetryRequest request, string accessToken, Action<ApiResult<TelemetryAckResponse>> completed) =>
            _coroutineRunner.StartCoroutine(_client.PostJson<ArSessionTelemetryRequest, TelemetryAckResponse>(
                "api/telemetry/ar-sessions", request, completed, accessToken));

        public void LogAffordabilitySession(AffordabilitySessionTelemetryRequest request, string accessToken, Action<ApiResult<TelemetryAckResponse>> completed) =>
            _coroutineRunner.StartCoroutine(_client.PostJson<AffordabilitySessionTelemetryRequest, TelemetryAckResponse>(
                "api/telemetry/affordability-sessions", request, completed, accessToken));
    }
}
