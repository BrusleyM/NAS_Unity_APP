using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class MoneyTests
    {
        [TestCase(0f, "R0")]
        [TestCase(950f, "R950")]
        [TestCase(1678f, "R1 678")]
        [TestCase(1250000f, "R1 250 000")]
        [TestCase(1677.6f, "R1 678")]
        [TestCase(-500f, "R-500")]
        public void RandsShowWithSpacesAndNoDecimals(float value, string expected)
        {
            Assert.AreEqual(expected, Money.Rand(value));
        }

        [Test]
        public void TheFormatDoesNotDependOnTheDevicesCulture()
        {
            var original = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                var us = Money.Rand(1234567f);
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                var de = Money.Rand(1234567f);

                Assert.AreEqual("R1 234 567", us);
                Assert.AreEqual(us, de);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}
