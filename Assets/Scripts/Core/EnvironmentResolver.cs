using NAS.Core.Networking;
using UnityEngine;

namespace NAS.Core
{
    // Loads the ApiSettings and certificate rule for GameManager.CurrentEnvironment from Resources/Config.
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
            var resolved = ResolveFor(environment, logPrefix);

            if (!_loggedOnce && resolved.Settings != null)
            {
                _loggedOnce = true;
                Debug.Log($"[Environment] {environment} -> {resolved.Settings.BaseUrl}{(resolved.TrustAnyCertificate ? " (certificate override)" : "")}");
            }
            return resolved;
        }

        private static Resolved ResolveFor(AppEnvironment environment, string logPrefix)
        {
            var settings = Resources.Load<ApiSettings>("Config/ApiSettings." + environment);
            if (settings != null)
                return new Resolved(settings, EnvironmentPolicy.TrustAnyCertificate(environment));

            // No fallback: silently using another backend would look like a network problem.
            Debug.LogError($"{logPrefix} Resources/Config/ApiSettings.{environment}.asset is missing, so there is no backend address for {environment}.");
            return new Resolved(null, false);
        }
    }
}
