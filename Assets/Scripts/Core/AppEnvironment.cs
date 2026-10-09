namespace NAS.Core
{
    // Named AppEnvironment, not Environment, to avoid colliding with System.Environment.
    //
    // The three-way split everyone talks about: Dev = Local / ApiDomain / ApiIp,
    // Staging, Production. See EnvironmentPolicy for which one a build actually uses.
    public enum AppEnvironment
    {
        Local,
        ApiDomain,

        // Same nginx HTTPS proxy as ApiDomain, addressed by raw LAN IP
        // instead of the api.nas.test hostname - for physical-device testing
        // on a network where nothing resolves that hostname (e.g. a phone
        // hotspot without dnsmasq's device-DNS override set up). Needs its
        // own ApiSettings asset since the IP changes per network/hotspot -
        // update that asset's _baseUrl when the LAN IP changes, same as the
        // dnsmasq LAN IP is per-network.
        ApiIp,

        // Hosted backends. Unlike the three above (which all mean "a development setup on
        // my own machine or network"), these use real HTTPS certificates, so certificate
        // checking stays ON. Staging is a place to test a release before customers see it;
        // Production is the live backend. Their ApiSettings live in Resources/Config
        // (ApiSettings.Staging.asset / ApiSettings.Production.asset), so no scene wiring
        // is needed. Values are appended, never renumbered: the scene stores the number.
        Staging,
        Production
    }
}
