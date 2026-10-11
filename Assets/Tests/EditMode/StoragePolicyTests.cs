using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class StoragePolicyTests
    {
        [TestCase(AppEnvironment.Local)]
        [TestCase(AppEnvironment.ApiDomain)]
        [TestCase(AppEnvironment.ApiIp)]
        public void EveryDevelopmentSetupUsesTheDevBucketConfig(AppEnvironment environment)
        {
            Assert.AreEqual(StoragePolicy.DevConfigPath, StoragePolicy.ConfigResourcePath(environment));
        }

        [Test]
        public void StagingAndProductionEachHaveTheirOwnConfig()
        {
            Assert.AreEqual(StoragePolicy.StagingConfigPath, StoragePolicy.ConfigResourcePath(AppEnvironment.Staging));
            Assert.AreEqual(StoragePolicy.ProductionConfigPath, StoragePolicy.ConfigResourcePath(AppEnvironment.Production));
        }

        [Test]
        public void NoTwoEnvironmentGroupsShareAConfigSoABuildCanNeverReadAnotherEnvironmentsBucket()
        {
            var paths = new[] { StoragePolicy.DevConfigPath, StoragePolicy.StagingConfigPath, StoragePolicy.ProductionConfigPath };

            CollectionAssert.AllItemsAreUnique(paths);
        }

        [TestCase(true, false, AppEnvironment.Local, AppEnvironment.Local)]       // Editor/development build: whatever is selected
        [TestCase(false, true, AppEnvironment.Local, AppEnvironment.Staging)]     // release build with the staging define
        [TestCase(false, false, AppEnvironment.Local, AppEnvironment.Production)] // plain release build
        public void AReleaseBuildAlwaysUsesItsOwnEnvironmentsBucketWhateverWasSelectedInTheEditor(bool debug, bool staging, AppEnvironment selected, AppEnvironment effective)
        {
            var path = StoragePolicy.ConfigResourcePath(EnvironmentPolicy.Effective(selected, debug, staging));

            Assert.AreEqual(StoragePolicy.ConfigResourcePath(effective), path);
        }
    }
}
