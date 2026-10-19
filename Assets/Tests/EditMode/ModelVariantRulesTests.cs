using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class ModelVariantRulesTests
    {
        [TestCase("Wheels_B")]
        [TestCase("Wheels_B_FL")]
        [TestCase("Wheels_C_RR")]
        [TestCase("Interior_B")]
        [TestCase("Interior_B_DoorCard_FL")]
        [TestCase("Interior_C_Seat_FL")]
        public void EveryVariantExceptTheDefaultIsHidden(string node)
        {
            Assert.IsTrue(ModelVariantRules.IsHiddenByDefault(node));
        }

        [TestCase("Wheels_A")]
        [TestCase("Wheels_A_FL")]
        [TestCase("Interior_A")]
        [TestCase("Interior_A_DoorCard_RR")]
        public void TheDefaultVariantStaysVisible(string node)
        {
            Assert.IsFalse(ModelVariantRules.IsHiddenByDefault(node));
        }

        [TestCase("Body")]
        [TestCase("Door_FL")]
        [TestCase("Wheels")]                 // an older car's single wheel group
        [TestCase("Interior")]               // an older car's single interior
        [TestCase("Wheels_Sport")]           // a word, not a one-letter variant
        [TestCase("Interior_Bench")]
        [TestCase("Fixed_Tyre_FL")]
        [TestCase("Trim_Grille")]
        [TestCase("Dashboard")]
        [TestCase("")]
        [TestCase(null)]
        public void NodesThatAreNotVariantsAreNeverHidden(string node)
        {
            Assert.IsFalse(ModelVariantRules.IsHiddenByDefault(node));
        }
    }
}
