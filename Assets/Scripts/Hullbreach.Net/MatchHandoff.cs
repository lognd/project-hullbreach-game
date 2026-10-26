using System.Globalization;
using Hullbreach.Platform;

namespace Hullbreach.Net
{
    // What the connect flow needs from a made match: the id to present and where to connect (S29-3).
    // frob:doc docs/reference/hullbreach-net.md#matchmaking
    // frob:ticket 01M3DG5Y1Q243GK60T3RHV8HRV
    public readonly struct MatchHandoff
    {
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public MatchHandoff(string matchId, string host, ushort port, string opponentId)
        {
            MatchId = matchId;
            Host = host;
            Port = port;
            OpponentId = opponentId;
        }

        // UUID shared by both players; the game server reports the result under it.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public string MatchId { get; }

        // Host part of the platform's server_address, as given (name or IP, no DNS here).
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public string Host { get; }

        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public ushort Port { get; }

        // Empty when the platform did not send one.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public string OpponentId { get; }

        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public override string ToString() => $"match_id='{MatchId}' server={Host}:{Port}";

        // Builds a handoff from the queue's match fields; bad input is an error, never a throw.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public static ConfigResult<MatchHandoff> Parse(string matchId, string serverAddress, string opponentId = null)
        {
            if (string.IsNullOrWhiteSpace(matchId))
                return ConfigResult<MatchHandoff>.Fail("match has no match_id");
            if (string.IsNullOrWhiteSpace(serverAddress))
                return ConfigResult<MatchHandoff>.Fail("match has no server_address");

            string address = serverAddress.Trim();
            int colon = address.LastIndexOf(':');
            if (colon <= 0 || colon == address.Length - 1)
                return ConfigResult<MatchHandoff>.Fail($"server_address '{address}' is not host:port");
            string host = address.Substring(0, colon);
            if (host.IndexOfAny(new[] { ':', ' ', '/' }) >= 0)
                return ConfigResult<MatchHandoff>.Fail($"server_address '{address}' has an invalid host");
            if (!ushort.TryParse(address.Substring(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture,
                    out ushort port) || port == 0)
                return ConfigResult<MatchHandoff>.Fail($"server_address '{address}' has an invalid port");
            return ConfigResult<MatchHandoff>.Ok(new MatchHandoff(matchId.Trim(), host, port, opponentId ?? string.Empty));
        }
    }
}
