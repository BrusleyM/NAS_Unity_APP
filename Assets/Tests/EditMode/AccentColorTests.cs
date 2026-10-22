using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class AccentColorTests
    {
        [TestCase("#FF5733", 1f, 0x57 / 255f, 0x33 / 255f)]
        [TestCase("00D4FF", 0f, 0xD4 / 255f, 1f)]
        [TestCase("  #00d4ff ", 0f, 0xD4 / 255f, 1f)]
        public void AHexColourIsReadWithOrWithoutTheHash(string hex, float r, float g, float b)
        {
            Assert.IsTrue(AccentColor.TryParse(hex, out var pr, out var pg, out var pb));
            Assert.AreEqual((r, g, b), (pr, pg, pb));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("#FFF")]
        [TestCase("#GGGGGG")]
        [TestCase("red")]
        public void ASettingThatIsNotAHexColourIsRejected(string hex)
        {
            Assert.IsFalse(AccentColor.TryParse(hex, out _, out _, out _));
        }

        [TestCase(0f, 212 / 255f, 1f)]
        [TestCase(0f, 158 / 255f, 180 / 255f)]
        [TestCase(0f, 106 / 255f, 128 / 255f)]
        public void TheAppsCyanInAnyBrightnessIsRecognised(float r, float g, float b)
        {
            Assert.IsTrue(AccentColor.IsAccent(r, g, b));
        }

        [TestCase(1f, 1f, 1f)]
        [TestCase(0.75f, 0.75f, 0.75f)]
        [TestCase(0.05f, 0.05f, 0.08f)]
        [TestCase(1f, 0.3f, 0.2f)]
        [TestCase(0.2f, 0.9f, 0.3f)]
        public void WhiteGreyDarkAndOtherHuesAreLeftAlone(float r, float g, float b)
        {
            Assert.IsFalse(AccentColor.IsAccent(r, g, b));
        }

        [Test]
        public void TheDefaultCyanBecomesTheDealershipColour()
        {
            AccentColor.Recolor(0f, 212 / 255f, 1f, 1f, 0x57 / 255f, 0x33 / 255f, out var r, out var g, out var b);
            Assert.AreEqual(1f, r, 0.01f);
            Assert.AreEqual(0x57 / 255f, g, 0.01f);
            Assert.AreEqual(0x33 / 255f, b, 0.01f);
        }

        [Test]
        public void ADimmerShadeOfTheCyanBecomesADimmerShadeOfTheNewColour()
        {
            AccentColor.Recolor(0f, 106 / 255f, 128 / 255f, 1f, 0f, 0f, out var r, out var g, out var b);
            Assert.AreEqual(0.5f, r, 0.02f);
            Assert.AreEqual(0f, g, 0.01f);
            Assert.AreEqual(0f, b, 0.01f);
        }

        [Test]
        public void ADarkPrimaryColourIsLiftedSoItStaysVisible()
        {
            AccentColor.Recolor(0f, 212 / 255f, 1f, 0f, 0f, 0.2f, out _, out _, out var b);
            Assert.AreEqual(0.55f, b, 0.01f);
        }
    }
}
