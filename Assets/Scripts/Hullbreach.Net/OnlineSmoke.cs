using System;

namespace Hullbreach.Net
{
    // Engine-free rules of the headless online smoke test: a demo/testing aid
    // that proves Unity Services sign-in and Relay lobby creation without clicks.
    // frob:doc docs/reference/hullbreach-net.md#onlinesmoke
    public static class OnlineSmoke
    {
        // frob:doc docs/reference/hullbreach-net.md#onlinesmoke
        public const string EnvVar = "HULLBREACH_ONLINE_SMOKE";

        // Every smoke log line starts with this so scripts can grep the run.
        // frob:doc docs/reference/hullbreach-net.md#onlinesmoke
        public const string LogPrefix = "Hullbreach online:";

        // frob:doc docs/reference/hullbreach-net.md#onlinesmoke
        public static bool Requested(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string v = value.Trim().ToLowerInvariant();
            return v == "1" || v == "true" || v == "yes" || v == "host";
        }

        // Private and time-stamped, so a smoke lobby never shows up in anyone's
        // public list and two runs never collide.
        // frob:doc docs/reference/hullbreach-net.md#onlinesmoke
        public static string LobbyName(DateTime utc) => "Hullbreach smoke " + utc.ToString("yyyyMMdd-HHmmss");

        // Unity Authentication caches one anonymous player per profile, so two
        // copies on one computer need different profiles to be two players.
        // frob:doc docs/reference/hullbreach-net.md#onlinesmoke
        public const string ProfileArg = "-hullbreachProfile";

        // The -hullbreachProfile value if it is a legal Unity profile name
        // (letters, digits, - and _, at most 30), else null for the default.
        // frob:doc docs/reference/hullbreach-net.md#onlinesmoke
        public static string Profile(string[] args)
        {
            if (args == null) return null;
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (!string.Equals(args[i], ProfileArg, StringComparison.OrdinalIgnoreCase)) continue;
                string name = args[i + 1];
                if (name.Length == 0 || name.Length > 30) return null;
                foreach (char c in name)
                {
                    bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                    if (!ok) return null;
                }
                return name;
            }
            return null;
        }

        // frob:doc docs/reference/hullbreach-net.md#onlinesmoke
        public static string StatusLine(string status, string error) =>
            string.IsNullOrEmpty(error) ? $"{LogPrefix} {status}" : $"{LogPrefix} {status} ({error})";
    }
}
