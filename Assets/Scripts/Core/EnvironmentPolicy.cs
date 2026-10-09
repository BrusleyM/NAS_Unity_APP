namespace NAS.Core
{
    // Which backend a build talks to, as pure rules (no Unity types) so they can be unit-tested.
    //
    //  - Editor and Development builds honour whatever is selected on GameManager, so a developer
    //    can point at Local, the LAN proxy, Staging or Production.
    //  - A normal (release) build IGNORES that selection: it is Production, or Staging when the
    //    build was made with the scripting define NAS_STAGING_BUILD. A release build can therefore
    //    never ship pointing at localhost because someone forgot to flip a dropdown back.
    public static class EnvironmentPolicy
    {
        public static AppEnvironment Effective(AppEnvironment selected, bool isDebugBuild, bool stagingBuild)
        {
            if (isDebugBuild) return selected;
            return stagingBuild ? AppEnvironment.Staging : AppEnvironment.Production;
        }

        // The development setups: this Mac, or this Mac through the nginx proxy.
        public static bool IsDevelopment(AppEnvironment environment) =>
            environment == AppEnvironment.Local
            || environment == AppEnvironment.ApiDomain
            || environment == AppEnvironment.ApiIp;

        // Only the nginx proxy setups use a locally generated certificate that devices do not trust.
        // Hosted environments (Staging, Production) have real certificates, so they are always verified.
        public static bool TrustAnyCertificate(AppEnvironment environment) =>
            environment == AppEnvironment.ApiDomain || environment == AppEnvironment.ApiIp;
    }
}
