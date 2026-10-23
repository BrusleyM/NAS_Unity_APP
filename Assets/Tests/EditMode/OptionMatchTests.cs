using System.Linq;
using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class OptionMatchTests
    {
        private static readonly OptionRef[] Wheels =
        {
            new OptionRef(1, "Style A", 0f),
            new OptionRef(2, "Style B", 9000f),
        };

        [TestCase("A", 1)]
        [TestCase("B", 2)]
        [TestCase("b", 2)]
        public void AWheelStyleIsFoundByTheLetterItsNameEndsIn(string letter, int id)
        {
            Assert.AreEqual(id, OptionMatch.ForVariant(Wheels, letter).Value.Id);
        }

        [TestCase("Wheels B")]
        [TestCase("B")]
        [TestCase("  20 inch Style B ")]
        public void AnyNameEndingInTheLetterMatches(string name)
        {
            var found = OptionMatch.ForVariant(new[] { new OptionRef(7, name, 0f) }, "B");
            Assert.AreEqual(7, found.Value.Id);
        }

        [Test]
        public void ALetterInsideAWordIsNotAMatch()
        {
            Assert.IsNull(OptionMatch.ForVariant(new[] { new OptionRef(1, "Alloy", 0f), new OptionRef(2, "Sport Tab", 0f) }, "B"));
        }

        [Test]
        public void ALetterTheCarDoesNotHaveFindsNothing()
        {
            Assert.IsNull(OptionMatch.ForVariant(Wheels, "C"));
            Assert.IsNull(OptionMatch.ForVariant(new OptionRef[0], "A"));
        }

        [Test]
        public void ATrimLevelIsFoundByTheGlowStylesNameIgnoringCase()
        {
            var levels = new[] { new OptionRef(1, "Original", 0f), new OptionRef(2, "Neon Cyan", 7500f) };

            Assert.AreEqual(2, OptionMatch.ForName(levels, "neon cyan").Value.Id);
            Assert.AreEqual(7500f, OptionMatch.ForName(levels, "Neon Cyan").Value.Price);
            Assert.IsNull(OptionMatch.ForName(levels, "Aurora"));
            Assert.IsNull(OptionMatch.ForName(levels, null));
        }

        [TestCase(9000f, "+R9 000")]
        [TestCase(7500f, "+R7 500")]
        [TestCase(1250000f, "+R1 250 000")]
        [TestCase(500f, "+R500")]
        [TestCase(0f, "")]
        [TestCase(-300f, "")]
        public void APriceTagShowsOnlyWhatCostsExtra(float price, string expected)
        {
            Assert.AreEqual(expected, OptionMatch.PriceTag(price));
        }

        [Test]
        public void TheSelectionSumsEveryPartsPrice()
        {
            var selection = new CustomizationSelection();
            selection.SetColour(10, 5000f);
            selection.SetWheel(2, 9000f);
            selection.SetInterior(4, 12000f);
            selection.SetTrim(6, 7500f);

            Assert.AreEqual(33500f, selection.OptionsTotal);
            Assert.AreEqual(2, selection.WheelOptionId);
            Assert.AreEqual(6, selection.TrimLevelId);
        }

        [Test]
        public void ChangingAPartReplacesItsPriceInsteadOfAddingToIt()
        {
            var selection = new CustomizationSelection();
            selection.SetWheel(2, 9000f);
            selection.SetWheel(1, 0f);

            Assert.AreEqual(0f, selection.OptionsTotal);
            Assert.AreEqual(1, selection.WheelOptionId);
        }

        [Test]
        public void ClearingForgetsEverything()
        {
            var selection = new CustomizationSelection();
            selection.SetColour(10, 5000f);
            selection.SetTrim(6, 7500f);

            selection.Clear();

            Assert.AreEqual(0f, selection.OptionsTotal);
            Assert.AreEqual(0, selection.ExteriorColorOptionId);
            Assert.AreEqual(0, selection.TrimLevelId);
        }
    }
}
