using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class DealershipBrandingTests
    {
        [TestCase(9.5f, 9.5f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(15f, 15f)]
        public void ARateTheDealershipSetIsWhereTheEstimatorStarts(float configured, float expected)
        {
            Assert.AreEqual(expected, DealershipBranding.StartingInterestRate(configured));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(15.1f)]
        public void ARateThatIsUnsetOrOutOfRangeKeepsTheDefaultStart(float configured)
        {
            Assert.AreEqual(DealershipBranding.FallbackInterestRate, DealershipBranding.StartingInterestRate(configured));
        }

        [Test]
        public void AFullLogoAddressIsUsedAsIs()
        {
            Assert.AreEqual("https://cdn.example.com/logo.png", DealershipBranding.ImageUrl(" https://cdn.example.com/logo.png ", "https://api.example.com"));
        }

        [Test]
        public void AnUploadedLogoPathGetsTheApiAddressInFront()
        {
            Assert.AreEqual("https://api.example.com/uploads/logo.png", DealershipBranding.ImageUrl("/uploads/logo.png", "https://api.example.com"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void NoLogoGivesNoAddress(string url)
        {
            Assert.IsNull(DealershipBranding.ImageUrl(url, "https://api.example.com"));
        }
    }
}
