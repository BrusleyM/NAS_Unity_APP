namespace NAS.Core.Networking
{
    // When does a server answer mean "your login is no longer valid"? Pure rule (no Unity types) so it is unit-tested.
    // 401 on a request that carried a bearer token = the token is expired or was rejected. A 401 on a request without one
    // is just a failed login (wrong password) and must NOT sign anybody out; wrong-current-password on change-password is a
    // 400 on the server, not a 401.
    public static class SessionExpiryPolicy
    {
        public const string Notice = "Your session expired. Please sign in again.";

        public static bool MeansSessionExpired(long statusCode, bool requestCarriedBearerToken) =>
            statusCode == 401 && requestCarriedBearerToken;
    }
}
