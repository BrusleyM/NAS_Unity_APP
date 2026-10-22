using System.Collections;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using NAS.Core.Events;
using NAS.Core.Interfaces;
using NAS.Core.Networking;
using UnityEngine;
using UnityEngine.Networking;

namespace NAS.Core
{
    // Cached cars are re-checked by ETag; a dropped download resumes; with no connection a cached car is still used.
    public sealed class ModelDownloader
    {
        private const int DownloadTimeoutSeconds = 180;   // models are up to ~60 MB, over a phone connection
        private const int MaxAttempts = 3;
        private const int MaxRestarts = 2;
        private static readonly float[] BackoffSeconds = { 1f, 3f };

        private readonly MonoBehaviour _runner;
        private readonly ModelDiskCache _cache;

        public ModelDownloader(MonoBehaviour coroutineRunner, ModelDiskCache cache)
        {
            _runner = coroutineRunner;
            _cache = cache;
        }

        public Task<Result<byte[]>> DownloadAsync(int vehicleId, string modelKey, string accessToken)
        {
            var done = new TaskCompletionSource<Result<byte[]>>();
            _runner.StartCoroutine(Run(vehicleId, modelKey, accessToken, done));
            return done.Task;
        }

        private IEnumerator Run(int vehicleId, string modelKey, string accessToken, TaskCompletionSource<Result<byte[]>> done)
        {
            var resolved = EnvironmentResolver.Resolve("[NAS Models]");
            if (resolved.Settings == null)
            {
                done.SetResult(Result<byte[]>.Failure("No backend address for this environment."));
                yield break;
            }

            var client = new ApiClient(resolved.Settings, resolved.TrustAnyCertificate);
            var hasCached = _cache.TryGet(modelKey, out var cachedPath, out var cachedEtag);
            var lastError = "Download failed.";
            var restarts = 0;

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                if (attempt > 0) yield return new WaitForSecondsRealtime(BackoffSeconds[attempt - 1]);

                ApiResult<ModelUrlResponse> link = default;
                var gotLink = false;
                yield return client.GetJson<ModelUrlResponse>($"api/customer/vehicles/{vehicleId}/model-url", r => { link = r; gotLink = true; }, accessToken);
                if (!gotLink || !link.Success)
                {
                    lastError = $"Could not get a download link ({(gotLink ? link.Error.ErrorCode ?? link.Error.StatusCode.ToString() : "none")}).";
                    var refused = gotLink && link.Error.StatusCode >= 400 && link.Error.StatusCode < 500 && link.Error.StatusCode != 429;
                    if (hasCached || refused) break;
                    continue;
                }

                var fetch = new Fetch();
                yield return Download(link.Value.url, modelKey, hasCached ? cachedEtag : null, fetch);

                if (fetch.Outcome == DownloadOutcome.Done)
                {
                    _cache.Commit(modelKey, fetch.Etag);
                    _cache.TryGet(modelKey, out cachedPath, out _);
                    done.SetResult(Result<byte[]>.Success(File.ReadAllBytes(cachedPath)));
                    yield break;
                }
                if (fetch.Outcome == DownloadOutcome.NotModified)
                {
                    done.SetResult(UseCached(modelKey, cachedPath));
                    yield break;
                }

                lastError = fetch.Error;
                if (fetch.Outcome == DownloadOutcome.Fatal) break;
                if (fetch.Outcome == DownloadOutcome.RestartFresh && restarts++ < MaxRestarts) attempt--;
            }

            done.SetResult(hasCached ? UseCached(modelKey, cachedPath) : Result<byte[]>.Failure(lastError));
        }

        private Result<byte[]> UseCached(string modelKey, string path)
        {
            _cache.Touch(modelKey);
            return Result<byte[]>.Success(File.ReadAllBytes(path));
        }

        private sealed class Fetch
        {
            public DownloadOutcome Outcome;
            public string Etag;
            public string Error;
        }

        private IEnumerator Download(string url, string modelKey, string cachedEtag, Fetch fetch)
        {
            var resumeFrom = _cache.PartLength(modelKey);
            var partEtag = _cache.PartEtag(modelKey);
            var resuming = resumeFrom > 0 && !string.IsNullOrEmpty(partEtag);
            if (!resuming)
            {
                _cache.DiscardPart(modelKey);
                resumeFrom = 0;
            }

            using var request = UnityWebRequest.Get(url);
            request.timeout = DownloadTimeoutSeconds;
            request.downloadHandler = new DownloadHandlerFile(_cache.PartPath(modelKey), resuming) { removeFileOnAbort = false };
            if (resuming)
            {
                request.SetRequestHeader("Range", $"bytes={resumeFrom}-");
                request.SetRequestHeader("If-Match", partEtag);
            }
            else if (!string.IsNullOrEmpty(cachedEtag))
            {
                request.SetRequestHeader("If-None-Match", cachedEtag);
            }

            var operation = request.SendWebRequest();
            var savedEtag = false;
            while (!operation.isDone)
            {
                if (request.responseCode == 200 || request.responseCode == 206)
                {
                    if (!savedEtag)
                    {
                        savedEtag = true;
                        _cache.SavePartEtag(modelKey, resuming ? partEtag : request.GetResponseHeader("ETag"));
                    }
                    long.TryParse(request.GetResponseHeader("Content-Length"), NumberStyles.None, CultureInfo.InvariantCulture, out var length);
                    var fromByte = request.responseCode == 206 ? resumeFrom : 0;
                    EventBus.Publish(new LoadingProgressEvent(ModelCacheRules.Progress(fromByte, (long)request.downloadedBytes, length)));
                }
                yield return null;
            }

            var networkFailed = request.result == UnityWebRequest.Result.ConnectionError;
            fetch.Outcome = ModelCacheRules.Classify(request.responseCode, networkFailed);
            fetch.Error = $"Model download failed ({request.responseCode}): {request.error}";
            fetch.Etag = request.GetResponseHeader("ETag");

            if (fetch.Outcome == DownloadOutcome.Done)
            {
                long.TryParse(request.GetResponseHeader("Content-Length"), NumberStyles.None, CultureInfo.InvariantCulture, out var length);
                var expected = (request.responseCode == 206 ? resumeFrom : 0) + length;
                // A server that ignored Range sent the whole file (appended to the old part), or the file is short.
                if ((resuming && request.responseCode == 200) || (length > 0 && _cache.PartLength(modelKey) != expected))
                    fetch.Outcome = DownloadOutcome.RestartFresh;
                else if (resuming)
                    fetch.Etag = partEtag;
            }

            if (fetch.Outcome != DownloadOutcome.Done && fetch.Outcome != DownloadOutcome.Retry)
                _cache.DiscardPart(modelKey);
        }
    }
}
