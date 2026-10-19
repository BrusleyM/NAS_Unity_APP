namespace NAS.Core
{
    // Named AppEnvironment, not Environment, to avoid colliding with System.Environment.
    //
    // Four places the app can point at. Each one's address lives in
    // Resources/Config/ApiSettings.<Name>.asset, so nothing is wired in a scene or
    // prefab. See EnvironmentPolicy for which one a build actually uses.
    //
    // The numbers are stored in the scene, so they never change: if a value is ever
    // removed its number is left unused, and new values are appended.
    public enum AppEnvironment
    {
        // This Mac's backend at http://localhost:5080. For the editor.
        Local = 0,

        // The local nginx HTTPS proxy, by hostname (api.nas.test). Only works on a device
        // that resolves that name (this Mac via /etc/hosts, or a phone via dnsmasq).
        ApiDomain = 1,

        // The same proxy, addressed by this Mac's LAN IP - for a phone on a network where
        // nothing resolves api.nas.test (e.g. a hotspot). Edit ApiSettings.ApiIp.asset
        // when the IP changes.
        ApiIp = 2,

        // The hosted staging backend, with a real HTTPS certificate (so certificate
        // checking stays ON). Every release build uses this until a production backend exists.
        Staging = 3
    }
}
