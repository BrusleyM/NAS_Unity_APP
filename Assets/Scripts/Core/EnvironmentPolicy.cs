namespace NAS.Core
{
    // Which backend a build talks to, as pure logic so it can be unit-tested.
    public static class EnvironmentPolicy
    {
        // Editor and development builds use the selection. A release build is always Staging, so it
        // cannot ship pointing at localhost. Add Production here when it exists.
        public static AppEnvironment Effective(AppEnvironment selected, bool isDebugBuild) =>
            isDebugBuild ? selected : AppEnvironment.Staging;

        // Only the nginx proxies use a locally generated certificate that devices do not trust.
        public static bool TrustAnyCertificate(AppEnvironment environment) =>
            environment == AppEnvironment.ApiDomain || environment == AppEnvironment.ApiIp;
    }
}
