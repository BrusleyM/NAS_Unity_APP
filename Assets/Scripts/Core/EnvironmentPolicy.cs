namespace NAS.Core
{
    // Which backend a build talks to, as pure rules (no Unity types) so they can be unit-tested.
    //
    //  - Editor and Development builds honour whatever is selected on GameManager.
    //  - A normal (release) build IGNORES that selection and uses Staging, so it can never
    //    ship pointing at localhost because someone forgot to flip a dropdown back.
    //    (When a production backend exists, add it as its own AppEnvironment and return it here.)
    public static class EnvironmentPolicy
    {
        public static AppEnvironment Effective(AppEnvironment selected, bool isDebugBuild) =>
            isDebugBuild ? selected : AppEnvironment.Staging;

        // Only the nginx proxy setups use a locally generated certificate that devices do not trust.
        // Staging has a real certificate, so it is always verified.
        public static bool TrustAnyCertificate(AppEnvironment environment) =>
            environment == AppEnvironment.ApiDomain || environment == AppEnvironment.ApiIp;
    }
}
