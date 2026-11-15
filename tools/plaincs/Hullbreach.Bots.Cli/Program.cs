using System.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Hullbreach.Bots;
using Hullbreach.World;

// Balance report for the bot arena. Usage:
//   dotnet run -c Release --project tools/plaincs/Hullbreach.Bots.Cli -- [--seeds N] [--seconds S]
//       [--storm] [--personalities] [--one EASY|NORMAL|HARD EASY|NORMAL|HARD SEED]
static class Program
{
    static int Main(string[] args)
    {
        int seeds = 20;
        float seconds = 150f;
        bool storm = false, personalities = false, trace = false; string only = null; string shipDiff = null;
        string[] one = null;
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seeds": seeds = int.Parse(args[++i]); break;
                case "--seconds": seconds = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                case "--trace": trace = true; break;
                case "--ships":
                    foreach (BotDifficulty d in new[] { BotDifficulty.Easy, BotDifficulty.Normal, BotDifficulty.Hard })
                        for (ulong sd = 1; sd <= 6; sd++)
                        {
                            BotShip bs = BotBuilder.Build(sd, d, BotPersonality.Balanced);
                            Console.WriteLine($"{d} seed {sd}: blocks {bs.BlockCount} thr {bs.Thrusters} gun {bs.Cannons} fin {bs.Fins} retro {bs.Retros} armor {bs.Armor}");
                        }
                    return 0;
                case "--only": only = args[++i]; break;
                case "--shipdiff": shipDiff = args[++i]; break;
                case "--storm": storm = true; break;
                case "--personalities": personalities = true; break;
                case "--one": one = new[] { args[++i], args[++i], args[++i] }; break;
                default: Console.Error.WriteLine("unknown option " + args[i]); return 2;
            }
        }

        StormConfig? stormConfig = storm ? StormConfig.Default() : (StormConfig?)null;
        if (one != null)
        {
            var cfg = new ArenaMatchConfig
            {
                Trace = trace ? (Action<string>)Console.WriteLine : null, Seed = ulong.Parse(one[2]), MaxSeconds = seconds, Storm = stormConfig,
                A = new ArenaSide { Difficulty = (BotDifficulty)Enum.Parse(typeof(BotDifficulty), one[0], true) },
                B = new ArenaSide { Difficulty = (BotDifficulty)Enum.Parse(typeof(BotDifficulty), one[1], true) },
            };
            if (shipDiff != null)
            {
                var sd = (BotDifficulty)Enum.Parse(typeof(BotDifficulty), shipDiff, true);
                cfg.A.Ship = BotBuilder.Build(cfg.Seed * 2 + 1, sd, BotPersonality.Balanced);
                cfg.B.Ship = BotBuilder.Build(cfg.Seed * 2 + 2, sd, BotPersonality.Balanced);
            }
            ArenaMatchResult r = ArenaMatch.Run(cfg);
            Console.WriteLine($"{r.LabelA} vs {r.LabelB} seed {r.Seed}: winner={r.Winner} ending={r.Ending} {r.Seconds:0.0}s " +
                $"shots {r.ShotsA}/{r.ShotsB} dmg {r.DamageA}/{r.DamageB} core {r.CoreHealthA:0.00}/{r.CoreHealthB:0.00} " +
                $"blocks {r.BlocksA}/{r.BlocksB} quiet {r.LongestQuietSeconds:0.0}s err={r.Error} simMs={r.SimMs:0} botMs={r.BotMs:0}");
            if (trace) Console.WriteLine("modes A " + string.Join(" ", Enumerable.Range(0, 8).Select(i => ((BotMode)i) + "=" + r.ModeTicksA[i])) + " | B " + string.Join(" ", Enumerable.Range(0, 8).Select(i => ((BotMode)i) + "=" + r.ModeTicksB[i])));
            return r.Ending == ArenaEnding.Crash ? 1 : 0;
        }

        var sw = Stopwatch.StartNew();
        List<ArenaBatch> batches = BalanceReport.RunStandard(seeds, seconds, stormConfig, personalities, only);
        Console.Write(BalanceReport.Format(batches,
            $"Hullbreach bot arena: {seeds} seeds per scenario, {seconds:0}s limit, storm {(storm ? "on" : "off")}"));
        Console.WriteLine($"(wall time {sw.Elapsed.TotalSeconds:0.0}s)");
        foreach (ArenaBatch b in batches)
            foreach (ArenaMatchResult m in b.Matches)
                if (m.Ending == ArenaEnding.Crash) Console.WriteLine($"CRASH {b.Name} seed {m.Seed}: {m.Error}");
        return 0;
    }
}
