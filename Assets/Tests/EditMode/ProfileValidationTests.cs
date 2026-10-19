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
    
        // --- sign-in identifier (email or username) ------------------------------------

        [TestCase("ada@example.com")]
        [TestCase("  ada@example.com  ")]
        [TestCase("ada4821")]
        [TestCase("ADA4821")]
        [TestCase("cool.ada_1")]
        public void AnEmailOrAUsernameIsAcceptedAsTheSignInName(string typed)
        {
            Assert.IsNull(ProfileValidation.ValidateLoginIdentifier(typed));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ABlankSignInNameIsRefused(string typed)
        {
            Assert.AreEqual("Enter your email or username.", ProfileValidation.ValidateLoginIdentifier(typed));
        }

        [TestCase("ada@")]
        [TestCase("@example.com")]
        [TestCase("ada@example")]
        [TestCase("a b@example.com")]
        public void SomethingWithAnAtSignMustBeAProperEmail(string typed)
        {
            Assert.AreEqual("Enter a valid email address.", ProfileValidation.ValidateLoginIdentifier(typed));
        }

        [TestCase("ab")]
        [TestCase("9lives")]
        [TestCase("has space")]
        [TestCase("bad-dash")]
        public void SomethingWithoutAnAtSignMustLookLikeAUsername(string typed)
        {
            Assert.IsNotNull(ProfileValidation.ValidateLoginIdentifier(typed));
        }

        // --- username (the one-time change) --------------------------------------------

        [TestCase("ada4821")]
        [TestCase("Cool.Ada")]
        [TestCase("  abc  ")]
        public void AUsernameOfTheRightShapeIsAccepted(string typed)
        {
            Assert.IsNull(ProfileValidation.ValidateUsername(typed));
        }

        [TestCase("")]
        [TestCase("ab")]
        [TestCase("1abc")]
        [TestCase("a@b")]
        [TestCase("émile")]
        [TestCase("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]   // 31 characters
        public void AUsernameOfTheWrongShapeIsRefused(string typed)
        {
            Assert.IsNotNull(ProfileValidation.ValidateUsername(typed));
        }

        [Test]
        public void ANormalisedUsernameIsTrimmedAndLowerCase()
        {
            Assert.AreEqual("cool.ada", ProfileValidation.NormalizeUsername("  Cool.ADA "));
            Assert.AreEqual(string.Empty, ProfileValidation.NormalizeUsername(null));
        }

        // --- the emailed code ----------------------------------------------------------

        [TestCase("123456", "123456")]
        [TestCase("12 34-56", "123456")]
        [TestCase("12ab34", "1234")]
        [TestCase("1234567890", "123456")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void ACodeBoxKeepsOnlyTheFirstSixDigits(string typed, string expected)
        {
            Assert.AreEqual(expected, ProfileValidation.NormalizeCode(typed));
        }

        [Test]
        public void OnlyAFullSixDigitCodeIsAccepted()
        {
            Assert.IsNull(ProfileValidation.ValidateCode("123456"));
            Assert.IsNull(ProfileValidation.ValidateCode("12 34 56"));
            Assert.IsNotNull(ProfileValidation.ValidateCode("12345"));
            Assert.IsNotNull(ProfileValidation.ValidateCode(""));
            Assert.IsNotNull(ProfileValidation.ValidateCode(null));
        }

        // --- password reset ------------------------------------------------------------

        [Test]
        public void AValidPasswordResetHasNoErrors()
        {
            Assert.IsEmpty(ProfileValidation.ValidatePasswordReset("123456", "BrandNewPass2!", "BrandNewPass2!"));
        }

        [Test]
        public void AResetReportsTheCodeAndPasswordsOnTheirOwnFields()
        {
            var errors = ProfileValidation.ValidatePasswordReset("12", "short", "different");

            Assert.AreEqual(3, errors.Count);
            Assert.IsTrue(errors.ContainsKey("code"));
            Assert.IsTrue(errors.ContainsKey("newPassword"));
            Assert.IsTrue(errors.ContainsKey("confirmPassword"));
        }

        [Test]
        public void AResetNeedsAConfirmationThatMatches()
        {
            Assert.IsTrue(ProfileValidation.ValidatePasswordReset("123456", "BrandNewPass2!", "").ContainsKey("confirmPassword"));
            Assert.IsTrue(ProfileValidation.ValidatePasswordReset("123456", "BrandNewPass2!", "BrandNewPass3!").ContainsKey("confirmPassword"));
        }
    }
}
