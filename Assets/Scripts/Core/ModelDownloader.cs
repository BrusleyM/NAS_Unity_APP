using System.Threading.Tasks;
using NAS.Core.Interfaces;
using NAS.Core.Networking;
using UnityEngine;
using UnityEngine.Networking;

namespace NAS.Core
{
    // Downloads a vehicle's 3D model in two steps: ask the API for a signed link, then fetch the file from that link.
    // The app never holds storage credentials, and the link expires in minutes (NAS_Backend/docs/STORAGE.md).
    public sealed class ModelDownloader
    {
        private const int DownloadTimeoutSeconds = 180;   // models are up to ~60 MB, over a phone connection

        private readonly MonoBehaviour _runner;

        public ModelDownloader(MonoBehaviour coroutineRunner) => _runner = coroutineRunner;

        public Task<Result<byte[]>> DownloadAsync(int vehicleId, string accessToken)
        {
            var done = new TaskCompletionSource<Result<byte[]>>();

            var resolved = EnvironmentResolver.Resolve("[NAS Models]");
            if (resolved.Settings == null)
            {
                done.SetResult(Result<byte[]>.Failure("No backend address for this environment."));
                return done.Task;
            }

            var client = new ApiClient(resolved.Settings, resolved.TrustAnyCertificate);
            _runner.StartCoroutine(client.GetJson<ModelUrlResponse>($"api/customer/vehicles/{vehicleId}/model-url", link =>
            {
                if (!link.Success)
                {
                    done.SetResult(Result<byte[]>.Failure($"Could not get a download link ({link.Error.ErrorCode ?? link.Error.StatusCode.ToString()}): {link.Error.UserMessage}"));
                    return;
                }
                _runner.StartCoroutine(Download(link.Value.url, done));
            }, accessToken));

            return done.Task;
        }

        private static System.Collections.IEnumerator Download(string url, TaskCompletionSource<Result<byte[]>> done)
        {
            // Plain request: the signature in the URL is the authorisation, so no bearer token and no certificate
            // override (the storage host has a real certificate).
            using var request = UnityWebRequest.Get(url);
            request.timeout = DownloadTimeoutSeconds;
            yield return request.SendWebRequest();

            done.SetResult(request.result == UnityWebRequest.Result.Success
                ? Result<byte[]>.Success(request.downloadHandler.data)
                : Result<byte[]>.Failure($"Model download failed ({request.responseCode}): {request.error}"));
        }
    }
}
