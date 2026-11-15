using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Hullbreach.Net;
using Hullbreach.World;

namespace Hullbreach.Bots
{
    // Aggregate of many matches between two fixed configurations.
    // frob:ticket 01M7EXX75QK41EKPV5392HXCAX
    // frob:doc docs/reference/hullbreach-bots.md#balancereport
    public sealed class ArenaBatch
    {
        public string Name;
        public string LabelA, LabelB;
        public readonly List<ArenaMatchResult> Matches = new List<ArenaMatchResult>();

        public int Count => Matches.Count;
        // Wins by the verdict: the real winner, or on a draw the side that dealt clearly more damage.
        public int WinsA => Tally(r => r.Verdict == 0);
        public int WinsB => Tally(r => r.Verdict == 1);
        public int Draws => Tally(r => r.Verdict < 0);
        // Wins that were actual core breaches or time-limit tiebreaks.
        public int HardWinsA => Tally(r => r.Winner == 0);
        public int HardWinsB => Tally(r => r.Winner == 1);
        public int Crashes => Tally(r => r.Ending == ArenaEnding.Crash);
        public int Stalls => Tally(r => r.Stalled);
        public int Unfinished => Tally(r => r.Ending == ArenaEnding.Unfinished);

        // Win share of A among decisive matches; 0.5 when there were none.
        public double ShareA => WinsA + WinsB == 0 ? 0.5 : (double)WinsA / (WinsA + WinsB);

        public double AverageSeconds
        {
            get
            {
                if (Matches.Count == 0) return 0;
                double sum = 0;
                foreach (var m in Matches) sum += m.Seconds;
                return sum / Matches.Count;
            }
        }

        // Match length percentile in seconds (0..1), over every match in the batch.
        public double SecondsAt(double q)
        {
            if (Matches.Count == 0) return 0;
            var list = new List<float>();
            foreach (var m in Matches) list.Add(m.Seconds);
            list.Sort();
            return list[(int)Math.Min(list.Count - 1, Math.Max(0, Math.Round(q * (list.Count - 1))))];
        }

        public int Ended(ArenaEnding e) => Tally(r => r.Ending == e);

        int Tally(Func<ArenaMatchResult, bool> pred)
        {
            int n = 0;
            foreach (var m in Matches) if (pred(m)) n++;
            return n;
        }
    }

    // The rules a sweep plays under, beyond the storm: economy, planet crash and whether the bots use the economy.
    // frob:doc docs/reference/hullbreach-bots.md#balancereport
    public sealed class ArenaRules
    {
        public EconomyConfig? Economy;
        public PlanetCrashConfig? Crash;
        public bool Quartermaster = true;
    }

    // Runs seed sweeps and prints the numbers that tuning needs: who wins, how long it takes, how it ends.
    // frob:doc docs/reference/hullbreach-bots.md#balancereport
    public static class BalanceReport
    {
        // Plays `seeds` matches of A vs B. Odd seeds swap the spawn slots so a slot advantage cancels out.
        // frob:doc docs/reference/hullbreach-bots.md#balancereport
        public static ArenaBatch RunBatch(string name, ArenaSide a, ArenaSide b, int seeds, float maxSeconds,
            StormConfig? storm, ulong firstSeed = 1, ArenaRules rules = null)
        {
            var batch = new ArenaBatch { Name = name, LabelA = a.Label, LabelB = b.Label };
            for (int i = 0; i < seeds; i++)
            {
                ulong seed = firstSeed + (ulong)i;
                var cfg = new ArenaMatchConfig
                {
                    Seed = seed, A = Clone(a), B = Clone(b), SwapSlots = (i & 1) == 1,
                    MaxSeconds = maxSeconds, Storm = storm,
                    Economy = rules?.Economy, Crash = rules?.Crash, Quartermaster = rules == null || rules.Quartermaster,
                    SameShip = a.Label == b.Label && a.Ship == null && b.Ship == null,
                };
                batch.Matches.Add(ArenaMatch.Run(cfg));
            }
            return batch;
        }

        static ArenaSide Clone(ArenaSide s) => new ArenaSide { Difficulty = s.Difficulty, Personality = s.Personality, Ship = s.Ship };

        // The standard scenario set: skill ladder, mirror, personality round robin.
        // frob:doc docs/reference/hullbreach-bots.md#balancereport
        public static List<ArenaBatch> RunStandard(int seeds, float maxSeconds, StormConfig? storm, bool includePersonalities, string only = null,
            ulong firstSeed = 1, ArenaRules rules = null)
        {
            var list = new List<ArenaBatch>();
            ArenaSide E = Side(BotDifficulty.Easy), N = Side(BotDifficulty.Normal), H = Side(BotDifficulty.Hard);
            void Add(string name, ArenaSide a, ArenaSide b)
            {
                if (only != null && name.IndexOf(only, StringComparison.OrdinalIgnoreCase) < 0) return;
                list.Add(RunBatch(name, a, b, seeds, maxSeconds, storm, firstSeed, rules));
            }
            Add("Hard vs Easy", H, E);
            Add("Hard vs Normal", H, N);
            Add("Normal vs Easy", N, E);
            Add("Mirror Easy", E, E);
            Add("Mirror Normal", N, N);
            Add("Mirror Hard", H, H);
            if (includePersonalities)
            {
                var types = (BotPersonality[])Enum.GetValues(typeof(BotPersonality));
                for (int i = 0; i < types.Length; i++)
                    for (int j = i + 1; j < types.Length; j++)
                        Add("Normal " + types[i] + " vs " + types[j], Side(BotDifficulty.Normal, types[i]), Side(BotDifficulty.Normal, types[j]));
            }
            return list;
        }

        static ArenaSide Side(BotDifficulty d, BotPersonality p = BotPersonality.Balanced)
            => new ArenaSide { Difficulty = d, Personality = p };

        // Plain-text table, one row per batch.
        // frob:doc docs/reference/hullbreach-bots.md#balancereport
        public static string Format(IList<ArenaBatch> batches, string title)
        {
            var sb = new StringBuilder();
            sb.AppendLine(title);
            sb.AppendLine("scenario                          n   A win  B win  even  A share  avg s   core  time  storm  stall  crash");
            foreach (ArenaBatch b in batches)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,-32} {1,3}  {2,5}  {3,5}  {4,4}  {5,6:P0}  {6,6:0.0}  {7,4}  {8,4}  {9,5}  {10,5}  {11,5}",
                    b.Name, b.Count, b.WinsA, b.WinsB, b.Draws, b.ShareA, b.AverageSeconds,
                    b.Ended(ArenaEnding.CoreBreached), b.Ended(ArenaEnding.TimeLimit), b.Ended(ArenaEnding.Storm),
                    b.Stalls, b.Crashes));
            }
            return sb.ToString();
        }

        // How matches ended and how long they ran: core breach, sudden death, time limit, draw, plus stalls and lengths.
        // frob:doc docs/reference/hullbreach-bots.md#balancereport
        public static string FormatEndings(IList<ArenaBatch> batches)
        {
            var sb = new StringBuilder();
            sb.AppendLine("scenario                          n  breach  sudden  limit  draw  stall   min    p25    p50    p75    max   mean");
            foreach (ArenaBatch b in batches)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,-32} {1,3}  {2,5}%  {3,5}%  {4,4}%  {5,3}%  {6,4}% {7,5:0} {8,6:0} {9,6:0} {10,6:0} {11,6:0} {12,6:0}",
                    b.Name, b.Count, Pct(b, b.Ended(ArenaEnding.CoreBreached)), Pct(b, b.Ended(ArenaEnding.Storm)),
                    Pct(b, b.Ended(ArenaEnding.TimeLimit)), Pct(b, b.Ended(ArenaEnding.Draw)), Pct(b, b.Stalls),
                    b.SecondsAt(0), b.SecondsAt(0.25), b.SecondsAt(0.5), b.SecondsAt(0.75), b.SecondsAt(1), b.AverageSeconds));
            }
            return sb.ToString();
        }

        static int Pct(ArenaBatch b, int n) => b.Count == 0 ? 0 : (int)Math.Round(100.0 * n / b.Count);
    }
}
