using System;
using System.Collections.Generic;
using Hullbreach.Platform;

namespace Hullbreach.Net
{
    // Everything the connect flow needs to join a queued match: where, which match, which skins (S29-3).
    // frob:doc docs/reference/hullbreach-net.md#connectrequest
    // frob:ticket 01M3DG5Y1Q243GK60T3RHV8HRV
    public sealed class ConnectRequest
    {
        // Caps on the untrusted skin list a caller hands in.
        // frob:doc docs/reference/hullbreach-net.md#connectrequest
        public const int MaxSkins = 16;

        // frob:doc docs/reference/hullbreach-net.md#connectrequest
        public const int MaxSkinIdLength = 64;

        ConnectRequest(string host, ushort port, string matchId, IReadOnlyList<string> skins)
        {
            Host = host;
            Port = port;
            MatchId = matchId;
            EquippedSkins = skins;
        }

        // Host as the platform gave it (name or IP); resolving it is the transport's job.
        // frob:doc docs/reference/hullbreach-net.md#connectrequest
        public string Host { get; }

        // frob:doc docs/reference/hullbreach-net.md#connectrequest
        public ushort Port { get; }

        // Canonical lowercase UUID; the id the server reports the result under.
        // frob:doc docs/reference/hullbreach-net.md#connectrequest
        public string MatchId { get; }

        // Cosmetic skin ids, de-duplicated, in the order given; empty means defaults.
        // frob:doc docs/reference/hullbreach-net.md#connectrequest
        public IReadOnlyList<string> EquippedSkins { get; }

        // One greppable line for the connect log; skin ids only, never a token.
        // frob:doc docs/reference/hullbreach-net.md#connectrequest
        public string Describe() =>
            $"Hullbreach connect: server={Host}:{Port} match_id={MatchId} skins=[{string.Join(",", EquippedSkins)}]";

        // Turns a made match plus the player's equipped skins into a request; bad input is an error value.
        // frob:doc docs/reference/hullbreach-net.md#connectrequest
        public static ConfigResult<ConnectRequest> FromHandoff(MatchHandoff handoff, IReadOnlyList<string> equippedSkins)
        {
            if (string.IsNullOrWhiteSpace(handoff.Host) || handoff.Port == 0)
                return ConfigResult<ConnectRequest>.Fail("handoff has no server address");
            ConfigResult<string> id = QueueMatchId.Normalize(handoff.MatchId);
            if (!id.IsOk) return ConfigResult<ConnectRequest>.Fail(id.Error);

            var skins = new List<string>();
            if (equippedSkins != null)
            {
                if (equippedSkins.Count > MaxSkins)
                    return ConfigResult<ConnectRequest>.Fail($"more than {MaxSkins} equipped skins");
                foreach (string raw in equippedSkins)
                {
                    string skin = raw == null ? string.Empty : raw.Trim();
                    if (skin.Length == 0 || skin.Length > MaxSkinIdLength)
                        return ConfigResult<ConnectRequest>.Fail($"skin id '{skin}' is empty or longer than {MaxSkinIdLength}");
                    foreach (char c in skin)
                        if (c < 0x21 || c > 0x7E)
                            return ConfigResult<ConnectRequest>.Fail($"skin id '{skin}' has a character outside printable ASCII");
                    if (!skins.Contains(skin)) skins.Add(skin);
                }
            }
            return ConfigResult<ConnectRequest>.Ok(new ConnectRequest(handoff.Host, handoff.Port, id.Value, skins));
        }
    }

    // Engine-free check and canonical form of a queue match id (a UUID), shared by client and server.
    // frob:doc docs/reference/hullbreach-net.md#connectrequest
    // frob:ticket 01M3DG5Y1Q243GK60T3RHV8HRV
    public static class QueueMatchId
    {
        // Lowercase hyphenated UUID, or an error; whitespace-trimmed, never throws.
        // frob:doc docs/reference/hullbreach-net.md#connectrequest
        public static ConfigResult<string> Normalize(string matchId)
        {
            if (string.IsNullOrWhiteSpace(matchId))
                return ConfigResult<string>.Fail("match has no match_id");
            if (!Guid.TryParseExact(matchId.Trim(), "D", out Guid guid) || guid == Guid.Empty)
                return ConfigResult<string>.Fail($"match_id '{matchId.Trim()}' is not a UUID");
            return ConfigResult<string>.Ok(guid.ToString("D"));
        }
    }
}
