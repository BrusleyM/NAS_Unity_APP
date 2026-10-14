using NAS.Core.Networking;
using NUnit.Framework;

namespace NAS.Tests
{
    public class SessionExpiryPolicyTests
    {
        [Test]
        public void AnUnauthorisedAnswerToARequestWithATokenMeansTheLoginExpired()
        {
            Assert.IsTrue(SessionExpiryPolicy.MeansSessionExpired(401, requestCarriedBearerToken: true));
        }

        [Test]
        public void AFailedLoginIsNotAnExpiredSession()
        {
            // Wrong password: 401 but nothing was signed in, so nobody is signed out.
            Assert.IsFalse(SessionExpiryPolicy.MeansSessionExpired(401, requestCarriedBearerToken: false));
        }

        [TestCase(0)]     // no connection
        [TestCase(200)]
        [TestCase(400)]   // includes a wrong current password on change-password
        [TestCase(403)]
        [TestCase(404)]
        [TestCase(409)]
        [TestCase(429)]
        [TestCase(500)]
        [TestCase(503)]
        public void NothingButA401ExpiresTheSession(long status)
        {
            Assert.IsFalse(SessionExpiryPolicy.MeansSessionExpired(status, requestCarriedBearerToken: true));
        }

        [Test]
        public void TheNoticeTellsTheCustomerWhatToDo()
        {
            StringAssert.Contains("sign in again", SessionExpiryPolicy.Notice);
        }
    }
}
