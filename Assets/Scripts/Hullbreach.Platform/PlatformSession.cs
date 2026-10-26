using System;

namespace Hullbreach.Platform
{
    // The signed-in player's platform session, in memory only for this run
    // (SessionTokenStore, S07-3, carries it across launches). The sign-in screen writes it; the client handshake reads it.
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

        // Demo/testing aid: a headless client cannot click the sign-in screen, so a
        // token from the environment (never a CLI arg) signs it in at startup.
        // frob:doc docs/platform-link.md#client-session
        public const string TokenEnv = "HULLBREACH_PLATFORM_TOKEN";

        // frob:doc docs/platform-link.md#client-session
        public const string UsernameEnv = "HULLBREACH_PLATFORM_USERNAME";

        // Signs in from TokenEnv/UsernameEnv when a token is set; logs the name, never the token.
        // frob:doc docs/platform-link.md#client-session
        public static bool SignInFromEnvironment(Func<string, string> getEnv, IPlatformLog log)
        {
            if (getEnv == null) throw new ArgumentNullException(nameof(getEnv));
            string token = (getEnv(TokenEnv) ?? string.Empty).Trim();
            if (token.Length == 0) return false;
            string username = (getEnv(UsernameEnv) ?? string.Empty).Trim();
            SignIn(username, token);
            log?.Info($"Hullbreach platform: signed in from {TokenEnv} as '{username}' (demo/testing aid).");
            return true;
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
