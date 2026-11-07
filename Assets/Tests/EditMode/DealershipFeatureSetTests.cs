using System.Collections.Generic;
using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class DealershipFeatureSetTests
    {
        private static DealershipFeatureSet Set(params (string key, bool enabled)[] states)
        {
            var list = new List<KeyValuePair<string, bool>>();
            foreach (var (key, enabled) in states) list.Add(new KeyValuePair<string, bool>(key, enabled));
            return DealershipFeatureSet.From(list);
        }

        [Test]
        public void NothingKnownMeansEverythingIsOn_SoABadConnectionNeverCostsTheCustomerAFeature()
        {
            Assert.IsTrue(DealershipFeatureSet.AllOn.EstimateAvailable);
            Assert.IsTrue(DealershipFeatureSet.AllOn.TestDriveAvailable);
            Assert.IsTrue(DealershipFeatureSet.AllOn.IsEnabled(DealershipFeatureSet.Ar));
            Assert.AreSame(DealershipFeatureSet.AllOn, DealershipFeatureSet.From(null));
            Assert.AreSame(DealershipFeatureSet.AllOn, DealershipFeatureSet.From(new List<KeyValuePair<string, bool>>()));
        }

        [Test]
        public void TheEstimateAndTheTestDriveFollowTheirOwnSwitch()
        {
            var financeOff = Set(("ar", true), ("finance", false), ("test_drive", true));
            var testDriveOff = Set(("ar", true), ("finance", true), ("test_drive", false));

            Assert.IsFalse(financeOff.EstimateAvailable);
            Assert.IsTrue(financeOff.TestDriveAvailable);
            Assert.IsTrue(testDriveOff.EstimateAvailable);
            Assert.IsFalse(testDriveOff.TestDriveAvailable);
        }

        [Test]
        public void ArIsNeverHidden_EvenIfTheServerSaysItIsOff()
        {
            var features = Set(("ar", false), ("finance", false), ("test_drive", false));

            Assert.IsTrue(features.IsEnabled(DealershipFeatureSet.Ar));
            Assert.IsFalse(features.EstimateAvailable);       // the others still follow the server
            Assert.IsFalse(features.TestDriveAvailable);
        }

        [Test]
        public void ASwitchOnTheServerDoesNotHideWhatItDoesNotMention()
        {
            var onlyFinanceOff = Set(("finance", false));

            Assert.IsTrue(onlyFinanceOff.TestDriveAvailable);               // not listed: on
            Assert.IsTrue(onlyFinanceOff.IsEnabled("something_added_later"));
            Assert.IsTrue(onlyFinanceOff.IsEnabled(null));
            Assert.IsTrue(onlyFinanceOff.IsEnabled(""));
        }

        [Test]
        public void KeysAreMatchedExactly()
        {
            var features = Set(("Finance", false), ("test-drive", false), ("", false));

            Assert.IsTrue(features.EstimateAvailable);          // "Finance" is not "finance"
            Assert.IsTrue(features.TestDriveAvailable);         // "test-drive" is not "test_drive"
        }

        [Test]
        public void AFeatureThatIsOnStaysOn()
        {
            Assert.IsTrue(Set(("finance", true), ("test_drive", true)).EstimateAvailable);
            Assert.AreSame(DealershipFeatureSet.AllOn, Set(("finance", true), ("test_drive", true), ("ar", true)));
        }
    }
}
