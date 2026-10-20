using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class EnvironmentPolicyTests
    {
        [TestCase(AppEnvironment.Local)]
        [TestCase(AppEnvironment.ApiDomain)]
        [TestCase(AppEnvironment.ApiIp)]
        [TestCase(AppEnvironment.Staging)]
        public void EditorAndDevelopmentBuildsUseWhateverIsSelected(AppEnvironment selected)
        {
            Assert.AreEqual(selected, EnvironmentPolicy.Effective(selected, isDebugBuild: true));
        }

        [TestCase(AppEnvironment.Local)]
        [TestCase(AppEnvironment.ApiDomain)]
        [TestCase(AppEnvironment.ApiIp)]
        [TestCase(AppEnvironment.Staging)]
        public void AReleaseBuildIgnoresTheSelectionAndIsStaging(AppEnvironment selected)
        {
            Assert.AreEqual(AppEnvironment.Staging, EnvironmentPolicy.Effective(selected, isDebugBuild: false));
        }

        [Test]
        public void OnlyTheLocalProxySetupsSkipCertificateChecking()
        {
            Assert.IsFalse(EnvironmentPolicy.TrustAnyCertificate(AppEnvironment.Local));
            Assert.IsTrue(EnvironmentPolicy.TrustAnyCertificate(AppEnvironment.ApiDomain));
            Assert.IsTrue(EnvironmentPolicy.TrustAnyCertificate(AppEnvironment.ApiIp));
            Assert.IsFalse(EnvironmentPolicy.TrustAnyCertificate(AppEnvironment.Staging));
        }

        [Test]
        public void ExistingEnvironmentNumbersAreUnchangedBecauseTheSceneStoresThem()
        {
            Assert.AreEqual(0, (int)AppEnvironment.Local);
            Assert.AreEqual(1, (int)AppEnvironment.ApiDomain);
            Assert.AreEqual(2, (int)AppEnvironment.ApiIp);
            Assert.AreEqual(3, (int)AppEnvironment.Staging);
        }
    }
}
