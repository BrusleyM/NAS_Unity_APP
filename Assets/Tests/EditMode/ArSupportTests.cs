using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class ArSupportTests
    {
        [TearDown]
        public void Reset() => ArSupport.Report(ArSupportProblem.None);

        [Test]
        public void NoProblemMeansNoMessage()
        {
            Assert.IsNull(ArSupport.MessageFor(ArSupportProblem.None));
        }

        [TestCase(ArSupportProblem.CameraPermissionDenied, "settings")]
        [TestCase(ArSupportProblem.DeviceNotSupported, "does not support")]
        [TestCase(ArSupportProblem.ServicesMissing, "Google Play Services for AR")]
        public void EveryProblemSaysWhatToDo(ArSupportProblem problem, string expected)
        {
            StringAssert.Contains(expected, ArSupport.MessageFor(problem));
        }

        [Test]
        public void TheLastAnswerIsKept()
        {
            ArSupport.Report(ArSupportProblem.ServicesMissing);
            Assert.AreEqual(ArSupportProblem.ServicesMissing, ArSupport.Last);
            ArSupport.Report(ArSupportProblem.None);
            Assert.AreEqual(ArSupportProblem.None, ArSupport.Last);
        }
    }
}
