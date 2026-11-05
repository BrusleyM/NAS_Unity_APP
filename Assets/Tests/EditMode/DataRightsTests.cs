using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class DataRightsTests
    {
        [TestCase(null)]
        [TestCase("")]
        public void ADeletionNeedsAPassword(string password)
        {
            Assert.AreEqual(DataRights.PasswordRequired, DataRights.ValidateDeletionPassword(password));
        }

        [Test]
        public void AnyTypedPasswordIsLeftForTheServerToCheck()
        {
            Assert.IsNull(DataRights.ValidateDeletionPassword("x"));
            Assert.IsNull(DataRights.ValidateDeletionPassword(" a password with spaces "));
        }

        [Test]
        public void ADeletionWithNothingElseToSaySaysOnlyThatTheAccountIsGone()
        {
            Assert.AreEqual("Your account has been deleted.", DataRights.DescribeDeletion(0, 0, null));
        }

        [Test]
        public void EnquiriesThatStayAreCountedAndSaidToHaveLostTheirDetails()
        {
            Assert.AreEqual(
                "Your account has been deleted. Your 1 enquiry to a dealership stays as a record without your name and contact details.",
                DataRights.DescribeDeletion(1, 0, null));
            Assert.AreEqual(
                "Your account has been deleted. Your 3 enquiries to dealerships stay as a record without your name and contact details.",
                DataRights.DescribeDeletion(3, 0, null));
        }

        [Test]
        public void ASaleThatIsKeptSaysUntilWhenInWords()
        {
            var text = DataRights.DescribeDeletion(0, 1, "2031-01-15");

            StringAssert.Contains("1 sale record is kept with your details until 15 January 2031", text);
            StringAssert.Contains("because the dealership may have to keep a record of a sale", text);
        }

        [Test]
        public void SalesThatAreKeptAreCountedInThePlural_AndAMissingDateIsNotInvented()
        {
            var text = DataRights.DescribeDeletion(2, 2, null);

            StringAssert.Contains("2 sale records are kept with your details until the dealership's retention period ends", text);
            StringAssert.Contains("Your 2 enquiries to dealerships stay", text);
        }

        [TestCase("2031-01-15", "15 January 2031")]
        [TestCase(" 2026-11-05 ", "5 November 2026")]
        public void ADateIsWrittenInWords(string iso, string expected)
        {
            Assert.AreEqual(expected, DataRights.FormatDate(iso));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("soon")]
        [TestCase("15/01/2031")]
        public void WhatIsNotADateIsNotFormatted(string iso)
        {
            Assert.IsNull(DataRights.FormatDate(iso));
        }

        [Test]
        public void TheCopyIsDescribedInCountsAndSingularsAreRight()
        {
            Assert.AreEqual(
                "Your copy holds your account details, 1 enquiry, 1 saved car and 1 record of how you used the app.",
                DataRights.DescribeExport(1, 1, 1, false));
            Assert.AreEqual(
                "Your copy holds your account details, 0 enquiries, 2 saved cars and 340 records of how you used the app.",
                DataRights.DescribeExport(0, 2, 340, false));
        }

        [Test]
        public void ACopyThatHitTheCapSaysSoAndSaysHowMuchItHolds()
        {
            var text = DataRights.DescribeExport(1, 1, 9000, true);

            StringAssert.Contains("up to 5,000 records", text);
            StringAssert.Contains("ask us for the rest", text);
        }
    }
}
