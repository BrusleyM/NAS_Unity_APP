using System;
using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class AccountRecoveryTests
    {
        private static readonly DateTime Sent = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void TheResendButtonIsLockedForSixtySecondsAfterACodeIsSent()
        {
            Assert.AreEqual(60, AccountRecovery.SecondsLeft(Sent, Sent));
            Assert.AreEqual(45, AccountRecovery.SecondsLeft(Sent, Sent.AddSeconds(15)));
            Assert.AreEqual(1, AccountRecovery.SecondsLeft(Sent, Sent.AddSeconds(59)));
        }

        [Test]
        public void ItUnlocksAtSixtySecondsAndStaysUnlocked()
        {
            Assert.AreEqual(0, AccountRecovery.SecondsLeft(Sent, Sent.AddSeconds(60)));
            Assert.AreEqual(0, AccountRecovery.SecondsLeft(Sent, Sent.AddMinutes(10)));
        }

        [Test]
        public void ARoundedUpPartialSecondStillCountsAsASecondToWait()
        {
            Assert.AreEqual(60, AccountRecovery.SecondsLeft(Sent, Sent.AddMilliseconds(100)));
            Assert.AreEqual(1, AccountRecovery.SecondsLeft(Sent, Sent.AddMilliseconds(59_500)));
        }

        [Test]
        public void AClockThatMovedBackwardNeverShowsMoreThanTheCooldown()
        {
            Assert.AreEqual(60, AccountRecovery.SecondsLeft(Sent, Sent.AddMinutes(-5)));
        }

        [Test]
        public void TheButtonTextShowsTheCountdownThenPlainResend()
        {
            Assert.AreEqual("Resend code in 42s", AccountRecovery.ResendLabel(42));
            Assert.AreEqual("Resend code", AccountRecovery.ResendLabel(0));
        }

        [TestCase("ada@example.com", "a**@example.com")]
        [TestCase("ab@example.com", "a**@example.com")]
        [TestCase("a@example.com", "a**@example.com")]
        [TestCase("ada.lovelace@example.com", "a******@example.com")]
        [TestCase("  ada@example.com ", "a**@example.com")]
        public void AnEmailIsMaskedToTheFirstLetterAndTheDomain(string email, string expected)
        {
            Assert.AreEqual(expected, AccountRecovery.MaskEmail(email));
        }

        [TestCase(null, "")]
        [TestCase("", "")]
        [TestCase("ada4821", "ada4821")]    // a username has nothing to mask
        public void NothingIsMaskedThatIsNotAnEmail(string typed, string expected)
        {
            Assert.AreEqual(expected, AccountRecovery.MaskEmail(typed));
        }

        [Test]
        public void AnAtSignMeansEmail()
        {
            Assert.IsTrue(AccountRecovery.LooksLikeEmail("ada@example.com"));
            Assert.IsFalse(AccountRecovery.LooksLikeEmail("ada4821"));
            Assert.IsFalse(AccountRecovery.LooksLikeEmail(null));
        }
    }
}
