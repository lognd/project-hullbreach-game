namespace Hullbreach.Net
{
    // Reduces a transport endpoint string to its host so a reconnecting player (new source port every time) compares equal.
    // Used by ServerSimulation.SetRemoteAddress as the LAN fallback identity.
    // frob:doc docs/reference/hullbreach-net.md#remoteaddress
    public static class RemoteAddress
    {
        // "1.2.3.4:7979" -> "1.2.3.4"; "[::1]:7979" -> "::1"; a bare host or an unbracketed IPv6 literal is returned as is.
        // Null or empty gives null (unknown).
        // frob:doc docs/reference/hullbreach-net.md#remoteaddress
        public static string HostOf(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint)) return null;
            if (endpoint[0] == '[')
            {
                int close = endpoint.IndexOf(']');
                return close > 1 ? endpoint.Substring(1, close - 1) : null;
            }
            int colon = endpoint.IndexOf(':');
            if (colon < 0) return endpoint;
            if (endpoint.IndexOf(':', colon + 1) >= 0) return endpoint; // several colons and no brackets: a bare IPv6 literal
            return colon == 0 ? null : endpoint.Substring(0, colon);
        }
    }
}
