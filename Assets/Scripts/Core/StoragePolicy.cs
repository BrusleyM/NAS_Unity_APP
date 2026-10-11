namespace NAS.Core
{
    // Which Tigris bucket (via which config asset in Resources/Config) a given app environment uses, as pure rules
    // (no Unity types) so they can be unit-tested, like EnvironmentPolicy.
    //
    // One bucket per environment, never shared (NAS_Backend/docs/STORAGE.md): database ids are only unique inside one
    // environment, and separate buckets mean separate access keys. The three development setups (this Mac, the nginx
    // proxy, a device on the LAN) all talk to the dev backend, so they share the dev bucket.
    public static class StoragePolicy
    {
        public const string DevConfigPath = "Config/DevStorageConfig";
        public const string StagingConfigPath = "Config/StagingStorageConfig";
        public const string ProductionConfigPath = "Config/ProductionStorageConfig";

        public static string ConfigResourcePath(AppEnvironment environment)
        {
            if (EnvironmentPolicy.IsDevelopment(environment)) return DevConfigPath;
            return environment == AppEnvironment.Staging ? StagingConfigPath : ProductionConfigPath;
        }
    }
}
