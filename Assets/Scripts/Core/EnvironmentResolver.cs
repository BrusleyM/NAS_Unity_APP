using NAS.Core.Networking;
using UnityEngine;

namespace NAS.Core
{
    // Resolves which ApiSettings + cert-trust to use from GameManager.CurrentEnvironment.
    // Every environment loads its ApiSettings from Resources/Config/ApiSettings.<Environment>.asset,
    // so no build depends on an Inspector reference that could be left unassigned. Shared by every
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

            // Never fall back to another environment here: a build silently talking to the wrong
            // backend would look like a network problem. A missing asset is a build defect - say so loudly.
            Debug.LogError($"{logPrefix} Resources/Config/ApiSettings.{environment}.asset is missing, so there is no backend address for {environment}.");
            return new Resolved(null, false);
        }
    }
}
