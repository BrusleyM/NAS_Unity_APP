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

        // --- learning from a refusal ---------------------------------------------------------

        [Test]
        public void ARefusalTurnsThatFeatureOffWhateverAnEarlierAnswerSaid()
        {
            var features = DealershipFeatureSet.AllOn.WithOff(DealershipFeatureSet.TestDrive);

            Assert.IsFalse(features.TestDriveAvailable);
            Assert.IsTrue(features.EstimateAvailable);                    // only the one that was refused
            Assert.IsTrue(DealershipFeatureSet.AllOn.TestDriveAvailable);   // the original set is not changed
        }

        [Test]
        public void RefusalsAddUp_AndTheSameOneTwiceChangesNothing()
        {
            var both = DealershipFeatureSet.AllOn.WithOff(DealershipFeatureSet.Finance).WithOff(DealershipFeatureSet.TestDrive);
            var once = DealershipFeatureSet.AllOn.WithOff(DealershipFeatureSet.Finance);

            Assert.IsFalse(both.EstimateAvailable);
            Assert.IsFalse(both.TestDriveAvailable);
            Assert.AreSame(once, once.WithOff(DealershipFeatureSet.Finance));
        }

        [Test]
        public void ArIsNeverTurnedOffByARefusalEither()
        {
            var features = DealershipFeatureSet.AllOn.WithOff(DealershipFeatureSet.Ar);

            Assert.IsTrue(features.IsEnabled(DealershipFeatureSet.Ar));
            Assert.AreSame(DealershipFeatureSet.AllOn, features);
        }

        [TestCase(null)]
        [TestCase("")]
        public void NoKeyChangesNothing(string key)
        {
            Assert.AreSame(DealershipFeatureSet.AllOn, DealershipFeatureSet.AllOn.WithOff(key));
        }

        [Test]
        public void AKnownOffStateSurvivesAFreshAllOnAnswerOnlyIfTheCallerKeepsIt()
        {
            // What GameManager relies on: a set that already has finance off keeps it through WithOff of something else.
            var known = Set(("finance", false)).WithOff(DealershipFeatureSet.TestDrive);

            Assert.IsFalse(known.EstimateAvailable);
            Assert.IsFalse(known.TestDriveAvailable);
        }

        // --- which answers still belong to the customer --------------------------------------

        [Test]
        public void AnAnswerAboutTheDealershipTheyAreStillLookingAtAndSignedInAppliesButNoOtherDoes()
        {
            Assert.IsTrue(DealershipFeatureSet.AnswerStillApplies(4, 4, "token"));
            Assert.IsFalse(DealershipFeatureSet.AnswerStillApplies(5, 4, "token"));       // chose another dealership meanwhile
            Assert.IsFalse(DealershipFeatureSet.AnswerStillApplies(null, 4, "token"));    // no dealership now
            Assert.IsFalse(DealershipFeatureSet.AnswerStillApplies(4, 4, null));          // signed out
            Assert.IsFalse(DealershipFeatureSet.AnswerStillApplies(4, 4, ""));
        }

        [Test]
        public void ARefreshedTokenDoesNotMatter_FeaturesBelongToTheDealershipNotTheToken()
        {
            Assert.IsTrue(DealershipFeatureSet.AnswerStillApplies(4, 4, "a-newer-token"));
        }
    }
}
