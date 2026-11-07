using NAS.Core.Networking;
using NUnit.Framework;

namespace NAS.Tests
{
    public class ApiErrorMessageTests
    {
        [Test]
        public void TheServersSentenceIsShownAsItIs()
        {
            Assert.AreEqual("Verify your email first.", new ApiError(400, "email_not_verified", "Verify your email first.", null).UserMessage);
        }

        [Test]
        public void NoAnswerAtAllReadsAsOffline()
        {
            Assert.AreEqual(ApiError.OfflineMessage, new ApiError(0, null, null, null).UserMessage);
        }

        [TestCase(404)]
        [TestCase(502)]
        public void AnAnswerWithoutASentenceGetsTheGenericMessage(long status)
        {
            StringAssert.StartsWith("Something went wrong", new ApiError(status, null, null, null).UserMessage);
        }
    }
}
