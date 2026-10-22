using System;
using System.Linq;
using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class ModelCacheRulesTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void ACacheFileNameIsSafeAndEndsInGlb()
        {
            var name = ModelCacheRules.FileName("dealerships/1/vehicles/NEO Lumen (v2).glb");

            StringAssert.EndsWith(".glb", name);
            StringAssert.DoesNotContain("/", name);
            StringAssert.DoesNotContain(" ", name);
            StringAssert.DoesNotContain("(", name);
        }

        [Test]
        public void TheSameKeyAlwaysGivesTheSameName()
        {
            Assert.AreEqual(ModelCacheRules.FileName("carmodels/a.glb"), ModelCacheRules.FileName("carmodels/a.glb"));
        }

        [Test]
        public void KeysThatLookAlikeStayApart()
        {
            Assert.AreNotEqual(ModelCacheRules.FileName("a/b.glb"), ModelCacheRules.FileName("a_b.glb"));
            Assert.AreNotEqual(ModelCacheRules.FileName("Car.glb"), ModelCacheRules.FileName("car.glb"));
        }

        [Test]
        public void ALongKeyGivesAShortName()
        {
            var name = ModelCacheRules.FileName("dealerships/" + new string('x', 400) + ".glb");
            Assert.LessOrEqual(name.Length, ModelCacheRules.MaxNameLength + 20);
        }

        [Test]
        public void NothingIsEvictedWhileEverythingFits()
        {
            var entries = new[] { new CacheEntry("a", 100, T0), new CacheEntry("b", 100, T0.AddMinutes(1)) };
            Assert.IsEmpty(ModelCacheRules.ToEvict(entries, 500, "b"));
        }

        [Test]
        public void TheLeastRecentlyUsedGoFirstUntilItFits()
        {
            var entries = new[]
            {
                new CacheEntry("new", 200, T0.AddHours(3)),
                new CacheEntry("old", 200, T0),
                new CacheEntry("mid", 200, T0.AddHours(1)),
            };

            var evict = ModelCacheRules.ToEvict(entries, 450, "new");

            CollectionAssert.AreEqual(new[] { "old" }, evict);
        }

        [Test]
        public void TheCarInUseIsNeverEvictedEvenIfItIsTheOldest()
        {
            var entries = new[] { new CacheEntry("inuse", 400, T0), new CacheEntry("other", 400, T0.AddHours(1)) };

            var evict = ModelCacheRules.ToEvict(entries, 500, "inuse");

            CollectionAssert.AreEqual(new[] { "other" }, evict);
        }

        [Test]
        public void ProgressCountsWhatAnEarlierAttemptAlreadySaved()
        {
            Assert.AreEqual(0.5f, ModelCacheRules.Progress(0, 50, 100), 0.001f);
            Assert.AreEqual(0.75f, ModelCacheRules.Progress(50, 25, 50), 0.001f);
        }

        [TestCase(0L, 0L, 0L)]
        [TestCase(0L, 10L, 0L)]
        public void ProgressIsZeroWhenTheSizeIsUnknown(long resumed, long downloaded, long length)
        {
            Assert.AreEqual(0f, ModelCacheRules.Progress(resumed, downloaded, length));
        }

        [Test]
        public void ProgressNeverPassesOneHundredPercent()
        {
            Assert.AreEqual(1f, ModelCacheRules.Progress(0, 500, 100));
        }

        [TestCase(200L, false, DownloadOutcome.Done)]
        [TestCase(206L, false, DownloadOutcome.Done)]
        [TestCase(304L, false, DownloadOutcome.NotModified)]
        [TestCase(412L, false, DownloadOutcome.RestartFresh)]
        [TestCase(416L, false, DownloadOutcome.RestartFresh)]
        [TestCase(0L, true, DownloadOutcome.Retry)]
        [TestCase(0L, false, DownloadOutcome.Retry)]
        [TestCase(403L, false, DownloadOutcome.Retry)]
        [TestCase(503L, false, DownloadOutcome.Retry)]
        [TestCase(404L, false, DownloadOutcome.Fatal)]
        [TestCase(400L, false, DownloadOutcome.Fatal)]
        public void EachAnswerHasOneOutcome(long status, bool networkFailed, DownloadOutcome expected)
        {
            Assert.AreEqual(expected, ModelCacheRules.Classify(status, networkFailed));
        }

        [Test]
        public void ADroppedConnectionIsRetriedEvenWithAPartialStatus()
        {
            Assert.AreEqual(DownloadOutcome.Retry, ModelCacheRules.Classify(200, networkFailed: true));
        }
    }
}
