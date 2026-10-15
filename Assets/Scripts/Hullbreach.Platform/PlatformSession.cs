namespace Hullbreach.Platform
{
    // The signed-in player's platform session, in memory only for this run (S07-3
    // owns persisting it). The sign-in screen writes it; the client handshake reads it.
    // frob:doc docs/platform-link.md#client-session
    public static class PlatformSession
    {
        static string s_token = string.Empty;
        static string s_username = string.Empty;

        // frob:doc docs/platform-link.md#client-session
        public static bool IsSignedIn => s_token.Length > 0;

        // frob:doc docs/platform-link.md#client-session
        public static string Username => s_username;

        // Secret: pass it to the handshake only, never to a log or the UI.
        // frob:doc docs/platform-link.md#client-session
        public static string Token => s_token;

        // frob:doc docs/platform-link.md#client-session
        public static void SignIn(string username, string token)
        {
            s_username = username ?? string.Empty;
            s_token = token ?? string.Empty;
        }

        // frob:doc docs/platform-link.md#client-session
        public static void SignOut()
        {
            s_username = string.Empty;
            s_token = string.Empty;
        }
    }
}
