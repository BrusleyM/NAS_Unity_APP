using NAS.Core;
using NAS.Core.Networking;
using NUnit.Framework;
using UnityEngine;

namespace NAS.Tests
{
    /// <summary>
    /// The server's answer is read with JsonUtility (a list of objects with a string and a bool), so the way it reads a real answer is tested here, in
    /// Unity, with a sample shaped like GET /api/customer/dealerships/{id}/features.
    /// </summary>
    public class DealershipFeaturesReaderTests
    {
        private static DealershipFeatureSet Read(string json) => DealershipFeaturesReader.ToFeatureSet(JsonUtility.FromJson<DealershipFeaturesResponse>(json));

        [Test]
        public void ARealAnswerWithTheEstimateSwitchedOffHidesOnlyTheEstimate()
        {
            var features = Read("{\"dealershipId\":4,\"features\":[{\"key\":\"ar\",\"enabled\":true},{\"key\":\"finance\",\"enabled\":false},{\"key\":\"test_drive\",\"enabled\":true}]}");

            Assert.IsFalse(features.EstimateAvailable);
            Assert.IsTrue(features.TestDriveAvailable);
            Assert.IsTrue(features.IsEnabled(DealershipFeatureSet.Ar));
        }

        [Test]
        public void BothSwitchedOffHidesBoth_AndArStaysOn()
        {
            var features = Read("{\"dealershipId\":4,\"features\":[{\"key\":\"ar\",\"enabled\":true},{\"key\":\"finance\",\"enabled\":false},{\"key\":\"test_drive\",\"enabled\":false}]}");

            Assert.IsFalse(features.EstimateAvailable);
            Assert.IsFalse(features.TestDriveAvailable);
            Assert.IsTrue(features.IsEnabled(DealershipFeatureSet.Ar));
        }

        [Test]
        public void AnAnswerThatSaysArIsOffStillLeavesArOn()
        {
            Assert.IsTrue(Read("{\"dealershipId\":4,\"features\":[{\"key\":\"ar\",\"enabled\":false}]}").IsEnabled(DealershipFeatureSet.Ar));
        }

        [TestCase("{\"dealershipId\":4,\"features\":[]}")]
        [TestCase("{\"dealershipId\":4}")]
        [TestCase("{}")]
        public void AnEmptyOrMissingListMeansEverythingOn(string json)
        {
            var features = Read(json);

            Assert.IsTrue(features.EstimateAvailable);
            Assert.IsTrue(features.TestDriveAvailable);
        }

        [Test]
        public void NoAnswerMeansEverythingOn()
        {
            var features = DealershipFeaturesReader.ToFeatureSet(null);

            Assert.IsTrue(features.EstimateAvailable);
            Assert.IsTrue(features.TestDriveAvailable);
        }

        [Test]
        public void AFeatureTheAppDoesNotKnowIsIgnoredAndDoesNotHideAnything()
        {
            var features = Read("{\"dealershipId\":4,\"features\":[{\"key\":\"hologram\",\"enabled\":false},{\"key\":\"finance\",\"enabled\":true}]}");

            Assert.IsTrue(features.EstimateAvailable);
            Assert.IsTrue(features.TestDriveAvailable);
        }
    }
}
