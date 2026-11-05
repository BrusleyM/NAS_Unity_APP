using UnityEngine;
using NAS.Core;
using NAS.Core.Auth;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// The lines every profile screen repeats before a call: the signed-in customer's access token, the environment's settings, and the
    /// API wrapper (created once per screen). False when there is no token or no settings, so the screen asks the person to sign in again.
    /// </summary>
    internal static class ProfileApiAccess
    {
        public static bool TryPrepare(MonoBehaviour owner, string logPrefix, ref ICustomerProfileApi api, out string accessToken)
        {
            accessToken = GameManager.Instance != null ? GameManager.Instance.AccessToken : null;
            var resolved = EnvironmentResolver.Resolve(logPrefix);
            if (resolved.Settings == null || string.IsNullOrEmpty(accessToken)) return false;
            api ??= new CustomerProfileApi(owner, resolved.Settings, resolved.TrustAnyCertificate);
            return true;
        }
    }
}
