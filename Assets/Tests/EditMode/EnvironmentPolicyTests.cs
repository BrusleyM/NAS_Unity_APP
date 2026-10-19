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

        // Each environment's address is loaded by name from Resources/Config, so every one needs its file.
        [TestCase(AppEnvironment.Local, "http://localhost:5080")]
        [TestCase(AppEnvironment.ApiDomain, "https://api.nas.test:8443")]
        [TestCase(AppEnvironment.Staging, "https://nas-api.neoxr.co.za")]
        public void EveryEnvironmentHasItsApiSettingsAsset(AppEnvironment environment, string expectedUrl)
        {
            var settings = UnityEngine.Resources.Load<NAS.Core.Networking.ApiSettings>("Config/ApiSettings." + environment);
            Assert.IsNotNull(settings, $"Resources/Config/ApiSettings.{environment}.asset is missing");
            Assert.AreEqual(expectedUrl, settings.BaseUrl);
        }

        [Test]
        public void TheLanIpEnvironmentHasItsAsset()
        {
            Assert.IsNotNull(UnityEngine.Resources.Load<NAS.Core.Networking.ApiSettings>("Config/ApiSettings." + AppEnvironment.ApiIp));
        }
    }
}
