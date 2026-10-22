using System;
using System.Collections.Generic;
using System.Text;

namespace NAS.Core
{
    public readonly struct CacheEntry
    {
        public readonly string Name;
        public readonly long Bytes;
        public readonly DateTime LastUsedUtc;

        public CacheEntry(string name, long bytes, DateTime lastUsedUtc)
        {
            Name = name;
            Bytes = bytes;
            LastUsedUtc = lastUsedUtc;
        }
    }

    public enum DownloadOutcome
    {
        Done,
        NotModified,
        // The partial file is unusable: start again from zero.
        RestartFresh,
        Retry,
        Fatal
    }

    public static class ModelCacheRules
    {
        public const int MaxNameLength = 80;

        // The hash keeps keys that sanitise to the same text apart.
        public static string FileName(string modelKey)
        {
            var key = (modelKey ?? string.Empty).Trim();
            var text = new StringBuilder(key.Length);
            foreach (var c in key.ToLowerInvariant())
                text.Append(char.IsLetterOrDigit(c) && c < 128 || c == '.' || c == '-' ? c : '_');
            var safe = text.Length > MaxNameLength ? text.ToString(text.Length - MaxNameLength, MaxNameLength) : text.ToString();
            return $"{safe}-{Hash(key):x8}.glb";
        }

        public static List<string> ToEvict(IEnumerable<CacheEntry> entries, long maxBytes, string keepName)
        {
            var all = new List<CacheEntry>(entries);
            long total = 0;
            foreach (var entry in all) total += entry.Bytes;

            all.Sort((a, b) => a.LastUsedUtc.CompareTo(b.LastUsedUtc));
            var chosen = new List<string>();
            foreach (var entry in all)
            {
                if (total <= maxBytes) break;
                if (entry.Name == keepName) continue;
                chosen.Add(entry.Name);
                total -= entry.Bytes;
            }
            return chosen;
        }

        public static float Progress(long resumedFrom, long downloadedNow, long contentLength)
        {
            var total = resumedFrom + contentLength;
            if (contentLength <= 0 || total <= 0) return 0f;
            return Math.Min(1f, (float)(resumedFrom + downloadedNow) / total);
        }

        public static DownloadOutcome Classify(long statusCode, bool networkFailed)
        {
            if (networkFailed || statusCode == 0) return DownloadOutcome.Retry;
            if (statusCode == 200 || statusCode == 206) return DownloadOutcome.Done;
            if (statusCode == 304) return DownloadOutcome.NotModified;
            if (statusCode == 412 || statusCode == 416) return DownloadOutcome.RestartFresh;
            // 403: the signed link ran out; a new one is the retry.
            if (statusCode == 403 || statusCode == 408 || statusCode == 429 || statusCode >= 500) return DownloadOutcome.Retry;
            return DownloadOutcome.Fatal;
        }

        private static uint Hash(string text)
        {
            var hash = 2166136261u;
            foreach (var c in text)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
