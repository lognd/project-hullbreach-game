using System;

namespace Hullbreach.Platform
{
    // Turns an online-service exception message into a sentence a player can act on.
    // Messages the game wrote itself pass through; anything else is mapped or replaced, never shown raw.
    // frob:doc docs/platform-link.md#screen-style
    // frob:ticket 01M6WV5HP3KVSZNJT6H99MFS3Z
    public static class FriendlyError
    {
        // Shown when nothing else matches; the raw text goes to the log, not the screen.
        public const string Generic = "Something went wrong with the online service. Try again.";

        // Appended when the services are down, since LAN play does not need them.
        public const string LanHint = " LAN play still works.";

        static readonly string[] OwnMessages =
        {
            "Enter a lobby code.",
            "Lobby passwords must be 8 to 64 characters.",
            "Only the lobby host can start the game.",
            "Another online operation is already in progress.",
            "A session ID is required.",
            "A gameplay scene is required.",
        };

        // Maps a raw message (null or empty is fine) to player text; servicesReady false adds the LAN hint.
        public static string From(string raw, bool servicesReady = true)
        {
            string text = Map(Strip(raw));
            return servicesReady ? text : text + LanHint;
        }

        // Drops the " (Parameter 'x')" suffix .NET appends to ArgumentException messages.
        static string Strip(string raw)
        {
            string text = (raw ?? string.Empty).Trim();
            int at = text.IndexOf(" (Parameter '", StringComparison.Ordinal);
            return at >= 0 ? text.Substring(0, at) : text;
        }

        static string Map(string text)
        {
            foreach (string own in OwnMessages)
            {
                if (text == own)
                {
                    return own;
                }
            }

            if (Has(text, "timed out") || Has(text, "timeout"))
            {
                return "The online service did not answer in time. Check your connection and try again.";
            }

            if (Has(text, "password"))
            {
                return "Wrong lobby password.";
            }

            if (Has(text, "full") || Has(text, "no available slots"))
            {
                return "That lobby is full.";
            }

            if (Has(text, "locked"))
            {
                return "That lobby has already started.";
            }

            if (Has(text, "not found") || Has(text, "404"))
            {
                return "No lobby found. Check the code and try again.";
            }

            if (Has(text, "too many") || Has(text, "429") || Has(text, "rate limit"))
            {
                return "Too many requests. Wait a few seconds and try again.";
            }

            if (Has(text, "network") || Has(text, "connect") || Has(text, "resolve") || Has(text, "unreachable")
                || Has(text, "internet") || Has(text, "dns"))
            {
                return "Cannot reach the online service. Check your internet connection.";
            }

            return Generic;
        }

        static bool Has(string text, string needle) =>
            text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
