namespace Hullbreach.Platform
{
    // The signed-in player's platform session, in memory only for this run (S07-3
    // owns persisting it). The sign-in screen writes it; the client handshake reads it.
    // frob:doc docs/platform-link.md#client-session
    public static class PlatformSession
    {
        static string s_token = string.Empty;
        static string s_username = string.Empty;
        static bool s_offline;

        // frob:doc docs/platform-link.md#client-session
        public static bool IsSignedIn => s_token.Length > 0;

        // True after the player chose "play offline" on the sign-in screen.
        // frob:doc docs/platform-link.md#client-session
        public static bool IsOffline => s_offline;

        // Signed in or offline: either way the sign-in screen has been answered this run.
        // frob:doc docs/platform-link.md#client-session
        public static bool HasChosen => IsSignedIn || s_offline;

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
            s_offline = false;
        }

        // Guest play: drops any token so the handshake hello carries none.
        // frob:doc docs/platform-link.md#client-session
        public static void PlayOffline()
        {
            s_username = string.Empty;
            s_token = string.Empty;
            s_offline = true;
        }

        // frob:doc docs/platform-link.md#client-session
        public static void SignOut()
        {
            s_username = string.Empty;
            s_token = string.Empty;
            s_offline = false;
        }
    }
}
