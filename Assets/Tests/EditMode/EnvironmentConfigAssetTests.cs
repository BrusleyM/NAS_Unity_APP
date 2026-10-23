using NAS.Core;
using NAS.Core.Networking;
using NUnit.Framework;
using UnityEngine;

namespace NAS.Tests
{
    // Unity-only (loads assets), so it is not part of the CI pure-logic project.
    public class EnvironmentConfigAssetTests
    {
        [TestCase(AppEnvironment.Local, "http://localhost:5080")]
        [TestCase(AppEnvironment.ApiDomain, "https://api.nas.test:8443")]
        [TestCase(AppEnvironment.Staging, "https://nas-api.neoxr.co.za")]
        public void EveryEnvironmentHasItsApiSettingsAsset(AppEnvironment environment, string expectedUrl)
        {
            var settings = Resources.Load<ApiSettings>("Config/ApiSettings." + environment);
            Assert.IsNotNull(settings, $"Resources/Config/ApiSettings.{environment}.asset is missing");
            Assert.AreEqual(expectedUrl, settings.BaseUrl);
        }

        [Test]
        public void TheLanIpEnvironmentHasItsAsset()
        {
            Assert.IsNotNull(Resources.Load<ApiSettings>("Config/ApiSettings." + AppEnvironment.ApiIp));
        }
    }
}
