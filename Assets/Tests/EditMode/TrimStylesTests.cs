using System.Linq;
using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class TrimStylesTests
    {
        [Test]
        public void TheFirstStyleIsTheCarsOwnTrim()
        {
            Assert.IsTrue(TrimStyles.All[0].IsOriginal);
            Assert.AreEqual("Original", TrimStyles.All[0].Name);
            Assert.AreEqual(1, TrimStyles.All.Count(s => s.IsOriginal));
        }

        [Test]
        public void EveryOtherStyleHasAColourAndAGlow()
        {
            foreach (var style in TrimStyles.All.Where(s => !s.IsOriginal))
            {
                StringAssert.IsMatch("^#[0-9A-Fa-f]{6}$", style.Hex, style.Name);
                Assert.Greater(style.Intensity, 0f, style.Name);
            }
        }

        [Test]
        public void SteadyPulsingAndFlowingStylesAreAllOffered()
        {
            var modes = TrimStyles.All.Where(s => !s.IsOriginal).Select(s => s.Mode).Distinct().ToList();
            CollectionAssert.Contains(modes, TrimMode.Steady);
            CollectionAssert.Contains(modes, TrimMode.Pulse);
            CollectionAssert.Contains(modes, TrimMode.Flow);
        }

        [Test]
        public void StyleNamesAreUnique()
        {
            CollectionAssert.AllItemsAreUnique(TrimStyles.All.Select(s => s.Name).ToList());
        }

        [Test]
        public void TheLabelNamesTheStyleAndHowItMoves()
        {
            Assert.AreEqual("Original", TrimStyles.All[0].Label);
            Assert.AreEqual("Aurora · Flow", TrimStyles.All.First(s => s.Name == "Aurora").Label);
        }
    }
}
