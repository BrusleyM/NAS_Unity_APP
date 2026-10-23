namespace NAS.Core
{
    // Named AppEnvironment to avoid colliding with System.Environment. Each value's address is
    // Resources/Config/ApiSettings.<Name>.asset. The numbers are stored in the scene: never renumber.
    public enum AppEnvironment
    {
        // http://localhost:5080, for the editor.
        Local = 0,

        // The local nginx HTTPS proxy by hostname (api.nas.test).
        ApiDomain = 1,

        // The same proxy by this Mac's LAN IP, for a phone that cannot resolve api.nas.test.
        // Edit ApiSettings.ApiIp.asset when the IP changes.
        ApiIp = 2,

        // The hosted staging backend (real certificate, so checking stays on).
        Staging = 3
    }
}
