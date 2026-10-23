using System;
using NAS.Core.Networking;
using NUnit.Framework;

namespace NAS.Tests
{
    public class SessionRefreshPolicyTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void ATokenWithPlentyOfTimeLeftIsLeftAlone()
        {
            Assert.IsFalse(SessionRefreshPolicy.NeedsRefresh("2026-10-20T12:30:00Z", Now));
        }

        [TestCase("2026-10-20T12:05:00Z")]   // exactly the lead time
        [TestCase("2026-10-20T12:02:00Z")]
        [TestCase("2026-10-20T11:00:00Z")]   // already lapsed
        public void ATokenThatLapsesSoonOrAlreadyHasIsRefreshed(string expires)
        {
            Assert.IsTrue(SessionRefreshPolicy.NeedsRefresh(expires, Now));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not a date")]
        public void AnUnreadableExpiryIsRefreshedToBeSafe(string expires)
        {
            Assert.IsTrue(SessionRefreshPolicy.NeedsRefresh(expires, Now));
        }

        [Test]
        public void TheServersTimestampIsReadAsUtc()
        {
            Assert.IsFalse(SessionRefreshPolicy.NeedsRefresh("2026-10-20T12:30:00.1234567Z", Now));
            Assert.IsFalse(SessionRefreshPolicy.NeedsRefresh("2026-10-20T12:30:00", Now));
        }

        [Test]
        public void ASavedRefreshTokenIsWorthTryingUntilItsOwnExpiry()
        {
            Assert.IsTrue(SessionRefreshPolicy.CanRestore("token", "2026-11-19T12:00:00Z", Now));
            Assert.IsFalse(SessionRefreshPolicy.CanRestore("token", "2026-10-20T11:59:00Z", Now));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void WithoutASavedRefreshTokenThereIsNothingToRestore(string token)
        {
            Assert.IsFalse(SessionRefreshPolicy.CanRestore(token, "2026-11-19T12:00:00Z", Now));
        }

        [Test]
        public void ASavedTokenWithAnUnreadableExpiryIsStillTried()
        {
            Assert.IsTrue(SessionRefreshPolicy.CanRestore("token", null, Now));
        }

        [Test]
        public void OnlyTheServerRefusingEndsTheLogin()
        {
            Assert.IsTrue(SessionRefreshPolicy.EndsSession(401));
            foreach (var status in new long[] { 0, 400, 403, 429, 500, 502, 503 })
                Assert.IsFalse(SessionRefreshPolicy.EndsSession(status), $"status {status}");
        }
    }
}
