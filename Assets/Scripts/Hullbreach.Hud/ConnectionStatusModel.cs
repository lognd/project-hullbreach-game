namespace Hullbreach.Hud
{
    // What the player should be told about their link to the match (UX P0-6).
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public enum ConnectionPhase
    {
        Hidden,      // connected and healthy, or not in a match
        Connecting,  // loading: waiting for the server to accept us
        Unstable,    // connected but the server has gone quiet: reconnect banner
        Lost,        // gone: failed to connect, timed out or the host left
    }

    // One thing to show: a title, a detail line and what the player can do about it.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public readonly struct ConnectionBanner
    {
        public readonly ConnectionPhase Phase;
        public readonly string Title;
        public readonly string Detail;
        public readonly bool OffersLobby;

        public ConnectionBanner(ConnectionPhase phase, string title, string detail, bool offersLobby)
        {
            Phase = phase; Title = title; Detail = detail; OffersLobby = offersLobby;
        }

        public static readonly ConnectionBanner None = new ConnectionBanner(ConnectionPhase.Hidden, string.Empty, string.Empty, false);
    }

    // Engine-free rules for the loading, reconnecting and disconnected states. The view feeds it what it can see each
    // frame; every failure gets plain words and a way back to the lobby, never a blank screen.
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public static class ConnectionStatusModel
    {
        public const double ConnectTimeoutSeconds = 10.0;
        public const double UnstableAfterSeconds = 2.0;
        public const double LostAfterSeconds = 8.0;

        // sinceEnteredMatch: seconds since the match scene loaded. sinceTraffic: seconds since the server last sent
        // anything (ignored while not connected). matchOver: the results screen is up, so a closing server is normal.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static ConnectionBanner Evaluate(bool inMatchScene, bool connected, bool everConnected,
            double sinceEnteredMatch, double sinceTraffic, bool matchOver)
        {
            if (!inMatchScene || matchOver) return ConnectionBanner.None;

            if (!connected)
            {
                if (everConnected)
                    return new ConnectionBanner(ConnectionPhase.Lost, "Disconnected",
                        "The host left the match, or the connection dropped.", true);
                if (sinceEnteredMatch >= ConnectTimeoutSeconds)
                    return new ConnectionBanner(ConnectionPhase.Lost, "Could not join the match",
                        "No answer from the host. Check the address and that the host is running, then try again.", true);
                return new ConnectionBanner(ConnectionPhase.Connecting, "Connecting...",
                    "Joining the match (" + Seconds(sinceEnteredMatch) + ")", false);
            }

            if (sinceTraffic >= LostAfterSeconds)
                return new ConnectionBanner(ConnectionPhase.Lost, "Connection lost",
                    "Nothing has arrived from the host for " + Seconds(sinceTraffic) + ".", true);
            if (sinceTraffic >= UnstableAfterSeconds)
                return new ConnectionBanner(ConnectionPhase.Unstable, "Connection unstable",
                    "Waiting for the host... " + Seconds(sinceTraffic), false);
            return ConnectionBanner.None;
        }

        static string Seconds(double s) => ((int)(s < 0 ? 0 : s)).ToString(System.Globalization.CultureInfo.InvariantCulture) + " s";
    }
}
