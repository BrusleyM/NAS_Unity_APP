using System;
using System.Collections;
using NAS.Core.Auth.Dtos;
using UnityEngine;
using UnityEngine.Networking;

namespace NAS.Core.Networking
{
    public sealed class ApiClient
    {
        private readonly ApiSettings _settings;
        private readonly bool _trustAnyCertificate;

        /// <param name="trustAnyCertificate">
        /// Skips UnityWebRequest's TLS certificate validation entirely via
        /// AcceptAllCertificatesHandler. UnityWebRequest validates against
        /// UnityTls's own bundled CA list, not the OS/Keychain trust store -
        /// so mkcert's locally-installed CA (enough for curl/Safari) has no
        /// effect here. Only ever pass true for the optional local nginx
        /// HTTPS proxy (NAS_Backend/nginx/README.md), never a real endpoint.
        /// </param>
        public ApiClient(ApiSettings settings, bool trustAnyCertificate = false)
        {
            _settings = settings;
            _trustAnyCertificate = trustAnyCertificate;
        }

        public IEnumerator PostJson<TRequest, TResponse>(string path, TRequest payload, Action<ApiResult<TResponse>> completed, string bearerToken = null)
        {
            var request = BuildRequest(path, UnityWebRequest.kHttpVerbPOST, JsonUtility.ToJson(payload), bearerToken);
            yield return request.SendWebRequest();
            Complete(request, completed);
        }

        public IEnumerator PutJson<TRequest, TResponse>(string path, TRequest payload, Action<ApiResult<TResponse>> completed, string bearerToken = null)
        {
            var request = BuildRequest(path, UnityWebRequest.kHttpVerbPUT, JsonUtility.ToJson(payload), bearerToken);
            yield return request.SendWebRequest();
            Complete(request, completed);
        }

        public IEnumerator GetJson<TResponse>(string path, Action<ApiResult<TResponse>> completed, string bearerToken = null)
        {
            var request = BuildRequest(path, UnityWebRequest.kHttpVerbGET, jsonBody: null, bearerToken);
            yield return request.SendWebRequest();
            Complete(request, completed);
        }

        private UnityWebRequest BuildRequest(string path, string method, string jsonBody, string bearerToken)
        {
            var request = new UnityWebRequest($"{_settings.BaseUrl}/{path.TrimStart('/')}", method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = _settings.TimeoutSeconds
            };

            if (jsonBody != null)
            {
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.SetRequestHeader("Accept", "application/json");

            if (!string.IsNullOrEmpty(bearerToken))
            {
                request.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
            }

            if (_trustAnyCertificate)
            {
                request.certificateHandler = new AcceptAllCertificatesHandler();
            }

            return request;
        }

        private static void Complete<TResponse>(UnityWebRequest request, Action<ApiResult<TResponse>> completed)
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                completed?.Invoke(ApiResult<TResponse>.FromSuccess(JsonUtility.FromJson<TResponse>(request.downloadHandler.text)));
            }
            else
            {
                completed?.Invoke(ApiResult<TResponse>.FromError(ParseError(request)));
            }
            request.Dispose();
        }

        private static ApiError ParseError(UnityWebRequest request)
        {
            var problem = string.IsNullOrWhiteSpace(request.downloadHandler?.text)
                ? null
                : JsonUtility.FromJson<ApiProblemDetailsDto>(request.downloadHandler.text);

            return new ApiError(
                request.responseCode,
                problem?.errorCode,
                problem?.detail ?? request.error,
                problem?.traceId);
        }
    }
}
