using System;
using System.IO;
using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class ModelDiskCacheTests
    {
        private string _dir;

        [SetUp]
        public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "nas-model-cache-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static void Download(ModelDiskCache cache, string key, int bytes, string etag)
        {
            File.WriteAllBytes(cache.PartPath(key), new byte[bytes]);
            cache.Commit(key, etag);
        }

        [Test]
        public void ACarThatWasNeverDownloadedIsNotCached()
        {
            var cache = new ModelDiskCache(_dir, 1000);
            Assert.IsFalse(cache.TryGet("a.glb", out _, out _));
        }

        [Test]
        public void ACommittedDownloadIsFoundWithItsEtag()
        {
            var cache = new ModelDiskCache(_dir, 1000);
            Download(cache, "a.glb", 100, "\"abc\"");

            Assert.IsTrue(cache.TryGet("a.glb", out var path, out var etag));
            Assert.AreEqual(100, new FileInfo(path).Length);
            Assert.AreEqual("\"abc\"", etag);
            Assert.IsFalse(File.Exists(cache.PartPath("a.glb")));
        }

        [Test]
        public void ANewDownloadReplacesTheCachedCopyAndItsEtag()
        {
            var cache = new ModelDiskCache(_dir, 1000);
            Download(cache, "a.glb", 100, "\"v1\"");
            Download(cache, "a.glb", 150, "\"v2\"");

            cache.TryGet("a.glb", out var path, out var etag);
            Assert.AreEqual(150, new FileInfo(path).Length);
            Assert.AreEqual("\"v2\"", etag);
        }

        [Test]
        public void ADownloadWithoutAnEtagLeavesNoStaleOne()
        {
            var cache = new ModelDiskCache(_dir, 1000);
            Download(cache, "a.glb", 100, "\"v1\"");
            Download(cache, "a.glb", 100, null);

            cache.TryGet("a.glb", out _, out var etag);
            Assert.IsNull(etag);
        }

        [Test]
        public void AnInterruptedDownloadKeepsItsPartAndEtagForResuming()
        {
            var cache = new ModelDiskCache(_dir, 1000);
            File.WriteAllBytes(cache.PartPath("a.glb"), new byte[40]);
            cache.SavePartEtag("a.glb", "\"abc\"");

            var again = new ModelDiskCache(_dir, 1000);
            Assert.AreEqual(40, again.PartLength("a.glb"));
            Assert.AreEqual("\"abc\"", again.PartEtag("a.glb"));
            Assert.IsFalse(again.TryGet("a.glb", out _, out _));
        }

        [Test]
        public void DiscardingAPartRemovesItAndItsEtag()
        {
            var cache = new ModelDiskCache(_dir, 1000);
            File.WriteAllBytes(cache.PartPath("a.glb"), new byte[40]);
            cache.SavePartEtag("a.glb", "\"abc\"");

            cache.DiscardPart("a.glb");

            Assert.AreEqual(0, cache.PartLength("a.glb"));
            Assert.IsNull(cache.PartEtag("a.glb"));
        }

        [Test]
        public void TheLeastRecentlyUsedCarsAreDeletedToStayUnderTheLimit()
        {
            var cache = new ModelDiskCache(_dir, 250);
            Download(cache, "old.glb", 100, "\"1\"");
            File.SetLastWriteTimeUtc(cache.TryGet("old.glb", out var oldPath, out _) ? oldPath : null, DateTime.UtcNow.AddDays(-2));
            Download(cache, "mid.glb", 100, "\"2\"");
            File.SetLastWriteTimeUtc(cache.TryGet("mid.glb", out var midPath, out _) ? midPath : null, DateTime.UtcNow.AddDays(-1));

            Download(cache, "new.glb", 100, "\"3\"");

            Assert.IsFalse(cache.TryGet("old.glb", out _, out _));
            Assert.IsFalse(File.Exists(oldPath + ".etag"));
            Assert.IsTrue(cache.TryGet("mid.glb", out _, out _));
            Assert.IsTrue(cache.TryGet("new.glb", out _, out _));
        }

        [Test]
        public void UsingACarMakesItTheLastToBeDeleted()
        {
            var cache = new ModelDiskCache(_dir, 250);
            Download(cache, "a.glb", 100, "\"1\"");
            cache.TryGet("a.glb", out var aPath, out _);
            File.SetLastWriteTimeUtc(aPath, DateTime.UtcNow.AddDays(-5));
            Download(cache, "b.glb", 100, "\"2\"");
            cache.TryGet("b.glb", out var bPath, out _);
            File.SetLastWriteTimeUtc(bPath, DateTime.UtcNow.AddDays(-3));

            cache.Touch("a.glb");
            Download(cache, "c.glb", 100, "\"3\"");

            Assert.IsTrue(cache.TryGet("a.glb", out _, out _));
            Assert.IsFalse(cache.TryGet("b.glb", out _, out _));
        }

        [Test]
        public void TheCarJustDownloadedSurvivesEvenWhenItAloneIsOverTheLimit()
        {
            var cache = new ModelDiskCache(_dir, 50);
            Download(cache, "big.glb", 100, "\"1\"");

            Assert.IsTrue(cache.TryGet("big.glb", out _, out _));
        }

        [Test]
        public void AbandonedPartsAreCleanedUpAfterAWeek()
        {
            var cache = new ModelDiskCache(_dir, 1000);
            File.WriteAllBytes(cache.PartPath("gone.glb"), new byte[10]);
            cache.SavePartEtag("gone.glb", "\"x\"");
            File.SetLastWriteTimeUtc(cache.PartPath("gone.glb"), DateTime.UtcNow.AddDays(-8));
            File.WriteAllBytes(cache.PartPath("fresh.glb"), new byte[10]);

            cache.Evict("other.glb");

            Assert.AreEqual(0, cache.PartLength("gone.glb"));
            Assert.AreEqual(10, cache.PartLength("fresh.glb"));
        }
    }
}
