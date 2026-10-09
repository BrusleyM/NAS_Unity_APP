using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    // Which backend a build talks to is the difference between a test and a customer's data,
    // so the rules get tests of their own.
    public class EnvironmentPolicyTests
    {
        [TestCase(AppEnvironment.Local)]
        [TestCase(AppEnvironment.ApiDomain)]
        [TestCase(AppEnvironment.ApiIp)]
        [TestCase(AppEnvironment.Staging)]
        [TestCase(AppEnvironment.Production)]
        public void EditorAndDevelopmentBuildsUseWhateverIsSelected(AppEnvironment selected)
        {
            Assert.AreEqual(selected, EnvironmentPolicy.Effective(selected, isDebugBuild: true, stagingBuild: false));
            Assert.AreEqual(selected, EnvironmentPolicy.Effective(selected, isDebugBuild: true, stagingBuild: true));
        }

        [TestCase(AppEnvironment.Local)]
        [TestCase(AppEnvironment.ApiDomain)]
        [TestCase(AppEnvironment.ApiIp)]
        [TestCase(AppEnvironment.Staging)]
        [TestCase(AppEnvironment.Production)]
        public void AReleaseBuildIgnoresTheSelectionAndIsProduction(AppEnvironment selected)
        {
            Assert.AreEqual(AppEnvironment.Production, EnvironmentPolicy.Effective(selected, isDebugBuild: false, stagingBuild: false));
        }

        [TestCase(AppEnvironment.Local)]
        [TestCase(AppEnvironment.Production)]
        public void AReleaseBuildMadeWithTheStagingDefineIsStagingWhateverIsSelected(AppEnvironment selected)
        {
            Assert.AreEqual(AppEnvironment.Staging, EnvironmentPolicy.Effective(selected, isDebugBuild: false, stagingBuild: true));
        }

        [Test]
        public void OnlyTheLocalProxySetupsSkipCertificateChecking()
        {
            Assert.IsFalse(EnvironmentPolicy.TrustAnyCertificate(AppEnvironment.Local));
            Assert.IsTrue(EnvironmentPolicy.TrustAnyCertificate(AppEnvironment.ApiDomain));
            Assert.IsTrue(EnvironmentPolicy.TrustAnyCertificate(AppEnvironment.ApiIp));
            Assert.IsFalse(EnvironmentPolicy.TrustAnyCertificate(AppEnvironment.Staging));
            Assert.IsFalse(EnvironmentPolicy.TrustAnyCertificate(AppEnvironment.Production));
        }

        [Test]
        public void HostedEnvironmentsAreNotDevelopmentOnes()
        {
            Assert.IsTrue(EnvironmentPolicy.IsDevelopment(AppEnvironment.Local));
            Assert.IsTrue(EnvironmentPolicy.IsDevelopment(AppEnvironment.ApiDomain));
            Assert.IsTrue(EnvironmentPolicy.IsDevelopment(AppEnvironment.ApiIp));
            Assert.IsFalse(EnvironmentPolicy.IsDevelopment(AppEnvironment.Staging));
            Assert.IsFalse(EnvironmentPolicy.IsDevelopment(AppEnvironment.Production));
        }

        [Test]
        public void ExistingEnvironmentNumbersAreUnchangedBecauseTheSceneStoresThem()
        {
            Assert.AreEqual(0, (int)AppEnvironment.Local);
            Assert.AreEqual(1, (int)AppEnvironment.ApiDomain);
            Assert.AreEqual(2, (int)AppEnvironment.ApiIp);
        }
    }
}
