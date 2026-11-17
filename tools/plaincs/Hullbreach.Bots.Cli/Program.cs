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
        string[] one = null; string tmplId = null; bool legacySpawn = false;
        bool idleB = false; ProxyKind proxyB = ProxyKind.None; bool proxyReport = false; string proxyDiff = "EASY";
        float?[] spawnOv = new float?[4]; float idleSeconds = 0f;
        ulong first = 1; bool noEcon = false, noCrash = false, noQm = false;
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
                case "--first": first = ulong.Parse(args[++i]); break;
                case "--noecon": noEcon = true; break;
                case "--template": tmplId = args[++i]; break;       // side A flies this starter template (id), e.g. balanced
                case "--legacy-spawn": legacySpawn = true; break;   // old orbit-ring spawn, a few units apart
                case "--spawn":   // sep,y,speed,deg (any may be '-' to keep the default), e.g. --spawn -,-,0,0 for the at-rest spawn
                    { string[] f = args[++i].Split(','); for (int k = 0; k < 4 && k < f.Length; k++) if (f[k] != "-") spawnOv[k] = float.Parse(f[k], System.Globalization.CultureInfo.InvariantCulture); }
                    break;
                case "--idle-b": idleB = true; break;
                case "--proxy": proxyReport = true; proxyB = (ProxyKind)Enum.Parse(typeof(ProxyKind), args[++i], true); break;   // moving-target probe: <difficulty> vs a proxy (nofire|orbit); see --proxy-diff
                case "--proxy-diff": proxyDiff = args[++i]; break;   // side B sends no input (a player who never touches the controls)
                case "--idle": idleSeconds = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;   // two idle ships on the spawns, report planet clearance
                case "--nocrash": noCrash = true; break;
                case "--noqm": noQm = true; break;
                case "--personalities": personalities = true; break;
                case "--one": one = new[] { args[++i], args[++i], args[++i] }; break;
                default: Console.Error.WriteLine("unknown option " + args[i]); return 2;
            }
        }

        StormConfig? stormConfig = storm ? StormConfig.Default() : (StormConfig?)null;
        var rules = new ArenaRules { Quartermaster = !noQm };
        rules.SpawnSeparation = spawnOv[0]; rules.SpawnLineY = spawnOv[1]; rules.SpawnSpeed = spawnOv[2]; rules.SpawnLaunchDegrees = spawnOv[3];
        if (noEcon) rules.Economy = Hullbreach.Net.EconomyConfig.Default().WithEnabled(false);
        if (noCrash) { var pc = PlanetCrashConfig.Default(); pc.DamagePerSpeed = 0f; pc.HeatDamagePerSecond = 0f; pc.HeatMaxDamagePerSecond = 0f; rules.Crash = pc; }
        if (idleSeconds > 0f) return IdleReport.Run(idleSeconds, spawnOv);
        if (proxyReport)
        {
            // Moving-target probe: side A (difficulty --proxy-diff) shoots at a Normal-skill proxy that never fires. Storm off unless --storm.
            Console.WriteLine("seed  first-hit  dmg@60s  dmg-total  shots  in-range%  mean-range  ending  core-B");
            for (int k = 0; k < seeds; k++)
            {
                ulong sd = first + (ulong)k;
                var pc = new ArenaMatchConfig
                {
                    Seed = sd, MaxSeconds = seconds, Storm = stormConfig, Economy = rules.Economy, Crash = rules.Crash, Quartermaster = rules.Quartermaster, SwapSlots = (k & 1) == 1,
                    A = new ArenaSide { Difficulty = (BotDifficulty)Enum.Parse(typeof(BotDifficulty), proxyDiff, true) },
                    B = new ArenaSide { Difficulty = BotDifficulty.Normal, Proxy = proxyB },
                };
                if (tmplId != null) pc.B.Ship = BotBuilder.FromTemplate(Hullbreach.Builder.ShipTemplates.ById(tmplId) ?? throw new ArgumentException("no template " + tmplId));
                ArenaMatchResult r = ArenaMatch.Run(pc);
                Console.WriteLine($"{sd,4}  {(r.FirstDamageOnBSeconds < 0 ? "never" : r.FirstDamageOnBSeconds.ToString("0.0")),9}  {r.DamageOnBAt60,7}  {r.DamageA,9}  {r.ShotsA,5}  {100.0 * r.InRangeTicks / Math.Max(1, r.Ticks),8:0}  {r.MeanRange,10:0.0}  {r.Ending}  {r.CoreHealthB:0.00}  modes " + string.Join(" ", Enumerable.Range(0, 8).Where(m => r.ModeTicksA[m] > 0).Select(m => ((BotMode)m).ToString().Substring(0, 3) + "=" + (100 * r.ModeTicksA[m] / Math.Max(1, r.Ticks)) + "%")) + " " + r.Error);
            }
            return 0;
        }
        if (one != null)
        {
            var cfg = new ArenaMatchConfig
            {
                Trace = trace ? (Action<string>)Console.WriteLine : null, Seed = ulong.Parse(one[2]), MaxSeconds = seconds, Storm = stormConfig, Economy = rules.Economy, Crash = rules.Crash, Quartermaster = rules.Quartermaster,
                A = new ArenaSide { Difficulty = (BotDifficulty)Enum.Parse(typeof(BotDifficulty), one[0], true) },
                B = new ArenaSide { Difficulty = (BotDifficulty)Enum.Parse(typeof(BotDifficulty), one[1], true) },
            };
            cfg.LegacySpawn = legacySpawn; cfg.B.Idle = idleB;
            cfg.SpawnSeparation = spawnOv[0]; cfg.SpawnLineY = spawnOv[1]; cfg.SpawnSpeed = spawnOv[2]; cfg.SpawnLaunchDegrees = spawnOv[3];
            if (tmplId != null) cfg.A.Ship = BotBuilder.FromTemplate(Hullbreach.Builder.ShipTemplates.ById(tmplId) ?? throw new ArgumentException("no template " + tmplId));
            if (shipDiff != null)
            {
                var sd = (BotDifficulty)Enum.Parse(typeof(BotDifficulty), shipDiff, true);
                cfg.A.Ship = BotBuilder.Build(cfg.Seed * 2 + 1, sd, BotPersonality.Balanced);
                cfg.B.Ship = BotBuilder.Build(cfg.Seed * 2 + 2, sd, BotPersonality.Balanced);
            }
            ArenaMatchResult r = ArenaMatch.Run(cfg);
            Console.WriteLine($"{r.LabelA} vs {r.LabelB} seed {r.Seed}: winner={r.Winner} ending={r.Ending} {r.Seconds:0.0}s " +
                $"shots {r.ShotsA}/{r.ShotsB} dmg {r.DamageA}/{r.DamageB} core {r.CoreHealthA:0.00}/{r.CoreHealthB:0.00} " +
                $"first shot {r.FirstShotSecondsA:0.0}/{r.FirstShotSecondsB:0.0}s blocks {r.BlocksA}/{r.BlocksB} quiet {r.LongestQuietSeconds:0.0}s inrange {100.0 * r.InRangeTicks / Math.Max(1, r.Ticks):0}% meanrange {r.MeanRange:0.0} err={r.Error} simMs={r.SimMs:0} botMs={r.BotMs:0}");
            if (trace) Console.WriteLine("modes A " + string.Join(" ", Enumerable.Range(0, 8).Select(i => ((BotMode)i) + "=" + r.ModeTicksA[i])) + " | B " + string.Join(" ", Enumerable.Range(0, 8).Select(i => ((BotMode)i) + "=" + r.ModeTicksB[i])));
            return r.Ending == ArenaEnding.Crash ? 1 : 0;
        }

        var sw = Stopwatch.StartNew();
        List<ArenaBatch> batches = BalanceReport.RunStandard(seeds, seconds, stormConfig, personalities, only, first, rules);
        Console.Write(BalanceReport.Format(batches,
            $"Hullbreach bot arena: {seeds} seeds per scenario, {seconds:0}s limit, storm {(storm ? "on" : "off")}"));
        Console.Write(BalanceReport.FormatEndings(batches));
        foreach (ArenaBatch b in batches)
            foreach (ArenaMatchResult m in b.Matches)
                Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "M,{0},{1},{2},{3:0.0},{4},{5},{6},{7},{8},{9:0.00},{10:0.00},{11},{12}", b.Name, m.Seed, m.Ending, m.Seconds, m.Winner, m.Verdict,
                    m.Stalled ? 1 : 0, m.DamageA, m.DamageB, m.CoreHealthA, m.CoreHealthB, m.BlocksA, m.BlocksB));
        Console.WriteLine($"(wall time {sw.Elapsed.TotalSeconds:0.0}s)");
        foreach (ArenaBatch b in batches)
            foreach (ArenaMatchResult m in b.Matches)
                if (m.Ending == ArenaEnding.Crash) Console.WriteLine($"CRASH {b.Name} seed {m.Seed}: {m.Error}");
        return 0;
    }
}

// Two idle ships (no input) on the match spawns for N seconds: when does each one first come within reach of a planet?
static class IdleReport
{
    public static int Run(float seconds, float?[] ov)
    {
        var d = Hullbreach.Net.ArenaConfig.Defaults();
        var arena = new Hullbreach.Net.ArenaConfig(d.Bodies, d.SpawnLateral, d.SpawnAltitude, d.SpawnVertical, d.SpawnAngleStep, d.Bounds, d.Storm, d.Crash, d.Economy,
            ov[0] ?? d.SpawnSeparation, ov[1] ?? d.SpawnLineY, ov[2] ?? d.SpawnSpeed, ov[3] ?? d.SpawnLaunchDegrees);
        var server = new Hullbreach.Net.ServerSimulation(new Hullbreach.Net.ServerOutbox()) { TickRate = 50f, MatchFeed = new Hullbreach.Platform.MatchEndFeed(), ReconnectGraceSeconds = 0f, TimeoutSeconds = 1e6f };
        server.ConfigureDemoArena(arena);
        var snap = new Hullbreach.Net.ShipSnapshot(0, 0, new[]
        {
            new Hullbreach.Net.SnapshotBlock(0, 0, Hullbreach.Core.BlockTypes.Core, 0, 0),
            new Hullbreach.Net.SnapshotBlock(0, 1, Hullbreach.Core.BlockTypes.Hull, 0, 0),
            new Hullbreach.Net.SnapshotBlock(0, -1, Hullbreach.Core.BlockTypes.Hull, 0, 0),
        }, 0, 0, 0, 0, 0, 0);
        server.Join(1, snap); server.Join(2, snap);
        var first = new float[3]; var minAlt = new float[3]; minAlt[1] = minAlt[2] = 1e9f; float minSep = 1e9f;
        int ticks = (int)(seconds * 50f);
        for (int t = 0; t <= ticks; t++)
        {
            for (int peer = 1; peer <= 2; peer++)
            {
                var p = server.Ships[peer].QueryPlanetProximity();
                if (p.Altitude < minAlt[peer]) minAlt[peer] = p.Altitude;
                if (first[peer] == 0f && (p.Altitude < 12f || p.AtmosphereDensity > 0f)) first[peer] = t / 50f;
            }
            minSep = System.Math.Min(minSep, Unity.Mathematics.math.distance(server.Ships[1].Position, server.Ships[2].Position));
            if (t % 500 == 0)
                System.Console.WriteLine($"t={t / 50f,5:0} A=({server.Ships[1].Position.x,6:0.0},{server.Ships[1].Position.y,6:0.0}) alt {server.Ships[1].QueryPlanetProximity().Altitude,5:0.0} | B=({server.Ships[2].Position.x,6:0.0},{server.Ships[2].Position.y,6:0.0}) alt {server.Ships[2].QueryPlanetProximity().Altitude,5:0.0} sep {Unity.Mathematics.math.distance(server.Ships[1].Position, server.Ships[2].Position),5:0.0}");
            server.Tick();
        }
        System.Console.WriteLine($"first within 12 of a surface / in atmosphere: A {(first[1] == 0 ? "never" : first[1].ToString("0.0") + "s")}, B {(first[2] == 0 ? "never" : first[2].ToString("0.0") + "s")}; min altitude A {minAlt[1]:0.0} B {minAlt[2]:0.0}; min ship separation {minSep:0.0}");
        return 0;
    }
}
