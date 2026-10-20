using System;
using System.Collections.Generic;
using System.IO;

namespace NAS.Core
{
    // <name>.glb + .etag, and a download in progress as <name>.glb.part (+ .etag) so it can resume.
    public sealed class ModelDiskCache
    {
        private static readonly TimeSpan StalePartAge = TimeSpan.FromDays(7);

        private readonly string _directory;
        private readonly long _maxBytes;

        public ModelDiskCache(string directory, long maxBytes)
        {
            _directory = directory;
            _maxBytes = maxBytes;
            Directory.CreateDirectory(_directory);
        }

        public bool TryGet(string modelKey, out string path, out string etag)
        {
            path = FinalPath(modelKey);
            etag = ReadText(path + ".etag");
            return File.Exists(path) && new FileInfo(path).Length > 0;
        }

        public void Touch(string modelKey)
        {
            var path = FinalPath(modelKey);
            if (File.Exists(path)) File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
        }

        public string PartPath(string modelKey) => FinalPath(modelKey) + ".part";

        public long PartLength(string modelKey) => File.Exists(PartPath(modelKey)) ? new FileInfo(PartPath(modelKey)).Length : 0;

        public string PartEtag(string modelKey) => ReadText(PartPath(modelKey) + ".etag");

        public void SavePartEtag(string modelKey, string etag)
        {
            if (!string.IsNullOrEmpty(etag)) File.WriteAllText(PartPath(modelKey) + ".etag", etag);
        }

        public void DiscardPart(string modelKey)
        {
            Delete(PartPath(modelKey));
            Delete(PartPath(modelKey) + ".etag");
        }

        public void Commit(string modelKey, string etag)
        {
            var path = FinalPath(modelKey);
            Delete(path);
            File.Move(PartPath(modelKey), path);
            Delete(PartPath(modelKey) + ".etag");
            if (!string.IsNullOrEmpty(etag)) File.WriteAllText(path + ".etag", etag);
            else Delete(path + ".etag");
            Evict(modelKey);
        }

        public void Evict(string keepModelKey)
        {
            var entries = new List<CacheEntry>();
            foreach (var file in new DirectoryInfo(_directory).GetFiles("*.glb"))
                entries.Add(new CacheEntry(file.Name, file.Length, file.LastWriteTimeUtc));

            foreach (var name in ModelCacheRules.ToEvict(entries, _maxBytes, ModelCacheRules.FileName(keepModelKey)))
            {
                Delete(Path.Combine(_directory, name));
                Delete(Path.Combine(_directory, name + ".etag"));
            }

            foreach (var part in new DirectoryInfo(_directory).GetFiles("*.part"))
            {
                if (DateTime.UtcNow - part.LastWriteTimeUtc <= StalePartAge) continue;
                Delete(part.FullName);
                Delete(part.FullName + ".etag");
            }
        }

        private string FinalPath(string modelKey) => Path.Combine(_directory, ModelCacheRules.FileName(modelKey));

        private static string ReadText(string path) => File.Exists(path) ? File.ReadAllText(path).Trim() : null;

        private static void Delete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
