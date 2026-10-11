using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class ProfileValidationTests
    {
        // --- profile -------------------------------------------------------------------

        [Test]
        public void AValidProfileHasNoErrors()
        {
            Assert.IsEmpty(ProfileValidation.ValidateProfile("Ada", "Lovelace", "+27 82 123 4567"));
        }

        [Test]
        public void BlankNamesAndCellNumberAreEachReportedOnTheirOwnField()
        {
            var errors = ProfileValidation.ValidateProfile("  ", "", null);

            Assert.AreEqual(3, errors.Count);
            Assert.IsTrue(errors.ContainsKey("firstName"));
            Assert.IsTrue(errors.ContainsKey("lastName"));
            Assert.IsTrue(errors.ContainsKey("cellNumber"));
        }

        [TestCase("abc")]
        [TestCase("12345")]                 // too short
        [TestCase("1234567890123456")]      // too long
        [TestCase("+27 82 123 45x7")]
        public void ACellNumberThatIsNotAPhoneNumberIsRefused(string cell)
        {
            var errors = ProfileValidation.ValidateProfile("Ada", "Lovelace", cell);

            Assert.AreEqual(1, errors.Count);
            Assert.IsTrue(errors.ContainsKey("cellNumber"));
        }

        [TestCase("0821234567")]
        [TestCase("+27821234567")]
        [TestCase("(021) 555-0110")]
        public void CommonSouthAfricanNumberFormatsAreAccepted(string cell)
        {
            Assert.IsEmpty(ProfileValidation.ValidateProfile("Ada", "Lovelace", cell));
        }

        [Test]
        public void ANameOver100CharactersIsRefusedBecauseTheServerWouldRefuseIt()
        {
            var errors = ProfileValidation.ValidateProfile(new string('a', 101), "Lovelace", "0821234567");

            Assert.IsTrue(errors.ContainsKey("firstName"));
        }

        // --- password change -----------------------------------------------------------

        [Test]
        public void AValidPasswordChangeHasNoErrors()
        {
            Assert.IsEmpty(ProfileValidation.ValidatePasswordChange("OldPassword1!", "BrandNewPass2!", "BrandNewPass2!"));
        }

        [Test]
        public void EmptyPasswordFieldsAreEachReported()
        {
            var errors = ProfileValidation.ValidatePasswordChange("", "", "");

            Assert.IsTrue(errors.ContainsKey("currentPassword"));
            Assert.IsTrue(errors.ContainsKey("newPassword"));
            Assert.IsTrue(errors.ContainsKey("confirmPassword"));
        }

        [Test]
        public void ANewPasswordUnderEightCharactersIsRefused()
        {
            var errors = ProfileValidation.ValidatePasswordChange("OldPassword1!", "short", "short");

            Assert.IsTrue(errors.ContainsKey("newPassword"));
            Assert.IsFalse(errors.ContainsKey("confirmPassword"));   // they do match, so only the length is wrong
        }

        [Test]
        public void TheNewPasswordMustDifferFromTheCurrentOne()
        {
            var errors = ProfileValidation.ValidatePasswordChange("SamePassword1!", "SamePassword1!", "SamePassword1!");

            Assert.IsTrue(errors.ContainsKey("newPassword"));
        }

        [Test]
        public void AMismatchedConfirmationIsReportedOnTheConfirmationField()
        {
            var errors = ProfileValidation.ValidatePasswordChange("OldPassword1!", "BrandNewPass2!", "BrandNewPass3!");

            Assert.AreEqual(1, errors.Count);
            Assert.IsTrue(errors.ContainsKey("confirmPassword"));
        }

        [Test]
        public void APasswordOver128CharactersIsRefusedBecauseTheServerWouldRefuseIt()
        {
            var tooLong = new string('a', 129);

            Assert.IsTrue(ProfileValidation.ValidatePasswordChange("OldPassword1!", tooLong, tooLong).ContainsKey("newPassword"));
        }

        // --- initials -------------------------------------------------------------------

        [TestCase("Ada Lovelace", "AL")]
        [TestCase("ada lovelace", "AL")]
        [TestCase("  Grace  Brewster Hopper ", "GH")]   // first and last word
        [TestCase("Cher", "C")]
        [TestCase("", "?")]
        [TestCase(null, "?")]
        [TestCase("   ", "?")]
        public void InitialsComeFromTheFirstAndLastWordOfTheName(string name, string expected)
        {
            Assert.AreEqual(expected, ProfileValidation.Initials(name));
        }
    }
}
