using NAS.Core.Networking;
using UnityEngine;

namespace NAS.Core
{
    // Resolves which ApiSettings + cert-trust to use from GameManager.CurrentEnvironment.
    // Development environments read their ApiSettings references from GameManager itself;
    // Staging and Production load theirs from Resources/Config so a release build never
    // depends on an Inspector reference that could be left unassigned. Shared by every
    // feature that needs to talk to the API, so this decision (and its config) lives in
    // exactly one place instead of being re-implemented (and able to drift) per caller.
    public static class EnvironmentResolver
    {
        private static bool _loggedOnce;

        public readonly struct Resolved
        {
            public readonly ApiSettings Settings;
            public readonly bool TrustAnyCertificate;

            public Resolved(ApiSettings settings, bool trustAnyCertificate)
            {
                Settings = settings;
                TrustAnyCertificate = trustAnyCertificate;
            }
        }

        public static Resolved Resolve(string logPrefix)
        {
            var gameManager = GameManager.Instance;
            var environment = gameManager != null ? gameManager.CurrentEnvironment : AppEnvironment.Local;
            var resolved = ResolveFor(environment, gameManager, logPrefix);

            if (!_loggedOnce && resolved.Settings != null)
            {
                _loggedOnce = true;
                Debug.Log($"[Environment] {environment} -> {resolved.Settings.BaseUrl}{(resolved.TrustAnyCertificate ? " (certificate override)" : "")}");
            }
            return resolved;
        }

        private static Resolved ResolveFor(AppEnvironment environment, GameManager gameManager, string logPrefix)
        {
            if (environment == AppEnvironment.Staging || environment == AppEnvironment.Production)
            {
                var hosted = Resources.Load<ApiSettings>("Config/ApiSettings." + environment);
                if (hosted != null)
                    return new Resolved(hosted, EnvironmentPolicy.TrustAnyCertificate(environment));

                // Never fall back to Local here: a hosted build silently talking to localhost
                // would look like a network problem. A missing asset is a build defect - say so loudly.
                Debug.LogError($"{logPrefix} Resources/Config/ApiSettings.{environment}.asset is missing, so there is no backend address for {environment}.");
                return new Resolved(null, false);
            }

            if (environment == AppEnvironment.ApiDomain)
            {
                if (gameManager != null && gameManager.ApiDomainSettings != null)
                    return new Resolved(gameManager.ApiDomainSettings, EnvironmentPolicy.TrustAnyCertificate(environment));

                Debug.LogWarning($"{logPrefix} GameManager.CurrentEnvironment is ApiDomain but the ApiDomain ApiSettings asset is not assigned. Falling back to local.");
            }

            if (environment == AppEnvironment.ApiIp)
            {
                if (gameManager != null && gameManager.ApiIpSettings != null)
                    return new Resolved(gameManager.ApiIpSettings, EnvironmentPolicy.TrustAnyCertificate(environment));

                Debug.LogWarning($"{logPrefix} GameManager.CurrentEnvironment is ApiIp but the ApiIp ApiSettings asset is not assigned. Falling back to local.");
            }

            return new Resolved(gameManager != null ? gameManager.ApiSettings : null, trustAnyCertificate: false);
        }
    }
}
