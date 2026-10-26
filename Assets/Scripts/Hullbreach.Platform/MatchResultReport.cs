using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hullbreach.Platform
{
    // One player's counters for the report; the platform stores them as non-negative ints.
    // frob:doc docs/platform-link.md#reporting-a-finished-match
    public readonly struct PlayerMatchStats
    {
        public readonly string UserId;
        public readonly int DamageDealt;
        public readonly int BlocksDestroyed;
        public readonly int BlocksPlaced;
        public readonly int TimeAliveSeconds;

        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public PlayerMatchStats(string userId, int damageDealt, int blocksDestroyed, int blocksPlaced,
            int timeAliveSeconds)
        {
            UserId = userId ?? string.Empty;
            DamageDealt = damageDealt;
            BlocksDestroyed = blocksDestroyed;
            BlocksPlaced = blocksPlaced;
            TimeAliveSeconds = timeAliveSeconds;
        }
    }

    // A finished two-player match as the platform's MatchReport expects it. The match id
    // is chosen by the game server once and reused on every resend (the idempotency key).
    // frob:doc docs/platform-link.md#reporting-a-finished-match
    public sealed class MatchResultReport
    {
        public readonly string MatchId;
        public readonly string WinnerId;
        public readonly int DurationSeconds;
        public readonly IReadOnlyList<PlayerMatchStats> Players;

        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public MatchResultReport(string matchId, string winnerId, int durationSeconds,
            IReadOnlyList<PlayerMatchStats> players)
        {
            MatchId = matchId ?? string.Empty;
            WinnerId = winnerId ?? string.Empty;
            DurationSeconds = durationSeconds;
            Players = players ?? Array.Empty<PlayerMatchStats>();
        }

        // Null when the report would pass the platform's validation, else why it would not.
        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public string Validate()
        {
            if (!IsUuid(MatchId)) return "match_id is not a uuid";
            if (!IsUuid(WinnerId)) return "winner_id is not a uuid";
            if (DurationSeconds < 0) return "duration_seconds is negative";
            if (Players.Count != 2) return "a match report needs exactly two players";
            for (int i = 0; i < 2; i++)
            {
                PlayerMatchStats p = Players[i];
                if (!IsUuid(p.UserId)) return "player " + i + " user_id is not a uuid";
                if (p.DamageDealt < 0 || p.BlocksDestroyed < 0 || p.BlocksPlaced < 0 || p.TimeAliveSeconds < 0)
                    return "player " + i + " has a negative counter";
            }
            if (Canonical(Players[0].UserId) == Canonical(Players[1].UserId))
                return "players must be two different users";
            string winner = Canonical(WinnerId);
            if (winner != Canonical(Players[0].UserId) && winner != Canonical(Players[1].UserId))
                return "winner_id must be one of the players";
            return null;
        }

        // The request body. Call only after Validate returned null.
        // frob:doc docs/platform-link.md#reporting-a-finished-match
        public string ToJson()
        {
            var b = new StringBuilder();
            b.Append("{\"match_id\":").Append(PlatformJson.Quote(Canonical(MatchId)));
            b.Append(",\"winner_id\":").Append(PlatformJson.Quote(Canonical(WinnerId)));
            b.Append(",\"duration_seconds\":").Append(DurationSeconds.ToString(CultureInfo.InvariantCulture));
            b.Append(",\"players\":[");
            for (int i = 0; i < Players.Count; i++)
            {
                PlayerMatchStats p = Players[i];
                if (i > 0) b.Append(',');
                b.Append("{\"user_id\":").Append(PlatformJson.Quote(Canonical(p.UserId)));
                b.Append(",\"damage_dealt\":").Append(p.DamageDealt.ToString(CultureInfo.InvariantCulture));
                b.Append(",\"blocks_destroyed\":").Append(p.BlocksDestroyed.ToString(CultureInfo.InvariantCulture));
                b.Append(",\"blocks_placed\":").Append(p.BlocksPlaced.ToString(CultureInfo.InvariantCulture));
                b.Append(",\"time_alive_seconds\":").Append(p.TimeAliveSeconds.ToString(CultureInfo.InvariantCulture));
                b.Append('}');
            }
            return b.Append("]}").ToString();
        }

        static bool IsUuid(string value) => Guid.TryParse(value, out _);

        // Lower-case hyphenated form, so a resend is byte-identical whatever casing came in.
        static string Canonical(string value) => Guid.Parse(value).ToString("D");
    }
}
