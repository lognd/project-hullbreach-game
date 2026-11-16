using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Match;
using Hullbreach.Net;
using Hullbreach.Platform;
using Hullbreach.Ship;
using Hullbreach.World;

namespace Hullbreach.Bots
{
    // How a headless match ended, from the harness's point of view.
    // frob:ticket 01M7EXX75QK41EKPV5392HXCAX
    // frob:doc docs/reference/hullbreach-bots.md#arenamatch
    public enum ArenaEnding
    {
        CoreBreached = 0,
        TimeLimit = 1,
        Storm = 2,
        Draw = 3,
        Unfinished = 4,
        Crash = 5,
    }

    // One side of an arena match: who plays it and with what ship.
    // frob:doc docs/reference/hullbreach-bots.md#arenamatch
    public sealed class ArenaSide
    {
        public BotDifficulty Difficulty = BotDifficulty.Normal;
        public BotPersonality Personality = BotPersonality.Balanced;

        // Null means "build one from the seed, difficulty and personality".
        public BotShip Ship;

        // True blinds the pilot to the storm (it still takes the damage); the baseline for measuring what awareness is worth.
        public bool IgnoreStorm;

        // True sends no input at all: a player who joined and never touched the controls (the idle-player probe).
        public bool Idle;

        public string Label => Difficulty + "/" + Personality;
    }

    // frob:doc docs/reference/hullbreach-bots.md#arenamatch
    public sealed class ArenaMatchConfig
    {
        public ulong Seed = 1;
        public ArenaSide A = new ArenaSide();
        public ArenaSide B = new ArenaSide();

        // When true B takes peer slot 1 (the spawn slot), so a batch can cancel the slot bias.
        public bool SwapSlots;

        // Gives B a copy of A's ship, so a mirror match isolates pilot and slot from the build.
        public bool SameShip;

        // Simulated match length; the server's own time limit is set to this.
        public float MaxSeconds = 150f;

        // Null runs without a storm; otherwise the real ServerSimulation storm runs this schedule and applies its damage.
        public StormConfig? Storm;

        // Null uses EconomyConfig.Default() (economy on). Pass Default().WithEnabled(false) for free building.
        public EconomyConfig? Economy;

        // Null uses PlanetCrashConfig.Default() (planet crash and atmosphere on).
        public PlanetCrashConfig? Crash;

        // False keeps the bots from placing, deconstructing or scavenging, to measure what the economy play is worth.
        public bool Quartermaster = true;

        // True spawns the ships on the old orbit ring a few units apart (the layout before the pacing change), for before/after runs.
        public bool LegacySpawn;

        // Optional overrides of the default spawn line (separation, height above the arena centre) and launch (speed, degrees from
        // "toward the centre"); null keeps ArenaConfig.Defaults(). A speed of 0 is the at-rest spawn of the pacing change.
        public float? SpawnSeparation, SpawnLineY, SpawnSpeed, SpawnLaunchDegrees;

        // Seconds without any damage that count as a stall.
        public float StallSeconds = 60f;

        // Optional once-per-two-seconds trace for tuning; null in tests and reports.
        public Action<string> Trace;
    }

    // frob:doc docs/reference/hullbreach-bots.md#arenamatch
    public sealed class ArenaMatchResult
    {
        public ulong Seed;
        public string LabelA, LabelB;

        // 0 = A, 1 = B, -1 = no winner.
        public int Winner = -1;

        // Who won on damage dealt when the match was not decided by a core breach: 0 = A, 1 = B, -1 = too close to call
        // (under a 15 percent lead or 50 points). A tied core-health time limit is a Draw to the server.
        public int PointsWinner = -1;

        // A core breach decides the match; otherwise the damage lead does. A time-limit core-health tiebreak
        // is left out of the verdict because a single lucky core hit is a noisy measure of skill.
        public int Verdict => Ending == ArenaEnding.CoreBreached || Ending == ArenaEnding.Storm ? Winner : PointsWinner;
        public ArenaEnding Ending = ArenaEnding.Unfinished;
        public float Seconds;
        public int Ticks;
        public float LongestQuietSeconds;
        public bool Stalled;
        public int ShotsA, ShotsB;
        // Seconds into the match of each side's first shot; -1 when it never fired.
        public float FirstShotSecondsA = -1f, FirstShotSecondsB = -1f;
        public int DamageA, DamageB;
        public float CoreHealthA, CoreHealthB;
        public int BlocksA, BlocksB;
        // Ticks each ship's centre of mass spent outside a damaging ring (sudden death not counted).
        public int OutsideTicksA, OutsideTicksB;
        public string Error;

        // Wall time spent in ServerSimulation.Tick versus in the bots, in milliseconds.
        public double SimMs, BotMs;

        // Ticks each pilot spent in each BotMode, for tuning.
        public readonly int[] ModeTicksA = new int[16], ModeTicksB = new int[16];
    }

    // Runs one bot-vs-bot match through the real ServerSimulation: same join validation, same input path,
    // same projectiles, contacts, structural solver and match state machine as an online game.
    // frob:doc docs/reference/hullbreach-bots.md#arenamatch
    public static class ArenaMatch
    {
        // frob:doc docs/reference/hullbreach-bots.md#arenamatch
        public static ArenaMatchResult Run(ArenaMatchConfig config)
        {
            var result = new ArenaMatchResult { Seed = config.Seed, LabelA = config.A.Label, LabelB = config.B.Label };
            try
            {
                RunCore(config, result);
            }
            catch (Exception e)
            {
                result.Ending = ArenaEnding.Crash;
                result.Winner = -1;
                result.Error = e.GetType().Name + ": " + e.Message;
            }
            return result;
        }

        static void RunCore(ArenaMatchConfig config, ArenaMatchResult result)
        {
            var master = new BotRng(config.Seed ^ 0xA5A5A5A5UL);
            BotShip shipA = config.A.Ship ?? BotBuilder.Build(config.Seed * 2 + 1, config.A.Difficulty, config.A.Personality);
            BotShip shipB = config.SameShip ? shipA : config.B.Ship ?? BotBuilder.Build(config.Seed * 2 + 2, config.B.Difficulty, config.B.Personality);

            var sim = new ServerSimulation(new DiscardOutbox())
            {
                TickRate = 50f,
                MatchTimeLimitSeconds = config.MaxSeconds,
                MatchFeed = new MatchEndFeed(),
                EmitPoseMessages = false,
                StormEnabled = config.Storm.HasValue,   // before Join: the match can start on the second join
                TimeoutSeconds = (config.A.Idle || config.B.Idle) ? 1e6f : 5f,   // an idle side sends nothing; keep the server default otherwise
            };

            // The match id seeds the storm ring and the pickup field (StormSeedFor). Left alone it is a fresh random uuid per
            // match, so the same Seed played a different fight every run. Derive it from the config instead.
            if (!sim.AssignQueueMatch(new Guid(0xA2E4A000u, 0, 0, (byte)(config.Seed >> 56), (byte)(config.Seed >> 48), (byte)(config.Seed >> 40), (byte)(config.Seed >> 32), (byte)(config.Seed >> 24), (byte)(config.Seed >> 16), (byte)(config.Seed >> 8), (byte)config.Seed).ToString("D"))) throw new InvalidOperationException("arena match id refused");

            int peerA = config.SwapSlots ? 2 : 1;
            int peerB = config.SwapSlots ? 1 : 2;
            // The real server configures the arena before anyone joins: the match, its storm and its economy start on the
            // second join, so they must already see this config. Configured again afterwards to place the ships.
            ArenaConfig defaults = ArenaConfig.Defaults();
            var arena = new ArenaConfig(defaults.Bodies, defaults.SpawnLateral, defaults.SpawnAltitude,
                defaults.SpawnVertical, defaults.SpawnAngleStep, defaults.Bounds, config.Storm ?? StormConfig.Default(),
                config.Crash ?? defaults.Crash, config.Economy ?? defaults.Economy, config.LegacySpawn ? 0f : config.SpawnSeparation ?? defaults.SpawnSeparation, config.LegacySpawn ? 0f : config.SpawnLineY ?? defaults.SpawnLineY,
                config.LegacySpawn ? 0f : config.SpawnSpeed ?? defaults.SpawnSpeed, config.SpawnLaunchDegrees ?? defaults.SpawnLaunchDegrees);
            sim.ConfigureDemoArena(arena);

            // Joined in peer order so slot 1 is always first in the arena's spawn order.
            var joins = new SortedDictionary<int, BotShip> { { peerA, shipA }, { peerB, shipB } };
            foreach (var kv in joins)
            {
                JoinResult jr = sim.Join(kv.Key, kv.Value.ToSnapshot((ushort)kv.Key, float2.zero, 0f, float2.zero));
                if (jr != JoinResult.Joined) throw new InvalidOperationException("join refused: " + jr);
            }
            sim.ConfigureDemoArena(arena);
            if (sim.Match == null || sim.Match.Phase != MatchPhase.Running) throw new InvalidOperationException("match did not start");

            var pilotA = new BotPilot(BotProfile.For(config.A.Difficulty, config.A.Personality), master.Fork(1));
            var pilotB = new BotPilot(BotProfile.For(config.B.Difficulty, config.B.Personality), master.Fork(2));
            var driverA = new BotPeer(peerA, peerB, pilotA);
            var driverB = new BotPeer(peerB, peerA, pilotB);

            IStormSource stormSource = sim.ActiveStorm != null ? new StormZoneSource(sim.ActiveStorm) : null;
            driverA.Quartermaster = config.Quartermaster ? new BotQuartermaster(shipA) : null;
            driverB.Quartermaster = config.Quartermaster ? new BotQuartermaster(shipB) : null;

            var seenShots = new HashSet<uint>();
            int maxTicks = (int)(config.MaxSeconds * sim.TickRate) + 200;
            int lastDamageTick = 0, longestQuiet = 0;
            int damageSignature = Signature(sim, peerA, peerB);
            uint tick = 0;
            for (int i = 1; i <= maxTicks; i++)
            {
                tick = (uint)i;
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                if (!config.A.Idle) driverA.Drive(sim, tick, config.A.IgnoreStorm ? null : stormSource);
                if (!config.B.Idle) driverB.Drive(sim, tick, config.B.IgnoreStorm ? null : stormSource);
                long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                sim.Tick();
                long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
                result.BotMs += (t1 - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                result.SimMs += (t2 - t1) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

                if (sim.ActiveStorm != null)
                {
                    StormState ring = sim.ActiveStorm.At(i);
                    if (ring.DamagePerTick > 0f && !ring.IsSuddenDeath)
                    {
                        if (!ring.Contains(CenterOfMass(sim.Ships[peerA]))) result.OutsideTicksA++;
                        if (!ring.Contains(CenterOfMass(sim.Ships[peerB]))) result.OutsideTicksB++;
                    }
                }
                result.ModeTicksA[(int)pilotA.Mode]++;
                result.ModeTicksB[(int)pilotB.Mode]++;
                foreach (var shot in sim.Projectiles)
                {
                    if (!seenShots.Add(shot.Id)) continue;
                    if (shot.OwnerPeer == peerA) { if (result.ShotsA++ == 0) result.FirstShotSecondsA = i / sim.TickRate; }
                    else if (shot.OwnerPeer == peerB) { if (result.ShotsB++ == 0) result.FirstShotSecondsB = i / sim.TickRate; }
                }

                if (config.Trace != null && i % 100 == 0) config.Trace(TraceLine(sim, i, peerA, peerB, pilotA, pilotB));
                int sig = Signature(sim, peerA, peerB);
                if (sig != damageSignature) { damageSignature = sig; longestQuiet = Math.Max(longestQuiet, i - lastDamageTick); lastDamageTick = i; }

                if (sim.Match.Phase == MatchPhase.Ended) break;
            }
            longestQuiet = Math.Max(longestQuiet, (int)tick - lastDamageTick);

            result.Ticks = (int)tick;
            result.Seconds = tick / sim.TickRate;
            result.LongestQuietSeconds = longestQuiet / sim.TickRate;
            result.Stalled = result.LongestQuietSeconds >= config.StallSeconds;

            ClassifyEnding(sim, result, peerA, peerB, (int)tick);
            if (result.Ending != ArenaEnding.CoreBreached && result.Ending != ArenaEnding.Storm)
            {
                int hi = Math.Max(result.DamageA, result.DamageB), lo = Math.Min(result.DamageA, result.DamageB);
                if (hi - lo >= 50 && hi > lo * 1.15f) result.PointsWinner = result.DamageA > result.DamageB ? 0 : 1;
            }
            var ships = sim.Ships;
            result.BlocksA = ships[peerA].Grid.Count;
            result.BlocksB = ships[peerB].Grid.Count;
            result.CoreHealthA = CoreHealth(ships[peerA].Grid);
            result.CoreHealthB = CoreHealth(ships[peerB].Grid);
        }

        static float2 CenterOfMass(ShipBody ship) => ship.LocalToWorld(ship.Grid.Mass.CenterOfMass);

        static string TraceLine(ServerSimulation sim, int tick, int peerA, int peerB, BotPilot pa, BotPilot pb)
        {
            ShipBody a = sim.Ships[peerA], b = sim.Ships[peerB];
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "t={0,5:0.0} A({1,6:0.0},{2,6:0.0}) v{3,5:0.0} {4,-11} B({5,6:0.0},{6,6:0.0}) v{7,5:0.0} {8,-11} range {9,5:0.0} blocks {10}/{11} accA {12:0.0}/{13:0.0} rot {14:0} req {15}/{16} cd {17:0.00} proj {18} err {19:0.00} st {20:0.00} w {21:0.00}",
                tick / 50f, a.Position.x, a.Position.y, math.length(a.Velocity), pa.Mode, b.Position.x, b.Position.y,
                math.length(b.Velocity), pb.Mode, math.distance(a.Position, b.Position), a.Grid.Count, b.Grid.Count,
                ShipCapabilities.Measure(a).MaxForwardAccel, ShipCapabilities.Measure(a).MaxAngularAccel, math.degrees(a.Rotation), pa.FireRequests, pb.FireRequests, a.CannonCooldown, sim.Projectiles.Count, pa.LastErr, pa.LastSteer, a.AngularVelocity);
        }

        static void ClassifyEnding(ServerSimulation sim, ArenaMatchResult result, int peerA, int peerB, int tick)
        {
            MatchStateMachine m = sim.Match;
            if (m.Phase != MatchPhase.Ended) { result.Ending = ArenaEnding.Unfinished; return; }
            if (m.EndedArgs.HasValue)
            {
                MatchEndedArgs args = m.EndedArgs.Value;
                int dmgPeerA = args.PeerA == peerA ? args.StatsA.DamageDealt : args.StatsB.DamageDealt;
                int dmgPeerB = args.PeerA == peerB ? args.StatsA.DamageDealt : args.StatsB.DamageDealt;
                result.DamageA = dmgPeerA;
                result.DamageB = dmgPeerB;
            }
            result.Winner = m.WinnerPeer == peerA ? 0 : m.WinnerPeer == peerB ? 1 : -1;
            switch (m.Result)
            {
                case MatchResult.CoreBreached:
                    // A breach once the last ring has closed is the storm's sudden death, not the cannons.
                    bool suddenDeath = sim.ActiveStorm != null && sim.ActiveStorm.At(tick).IsSuddenDeath;
                    result.Ending = suddenDeath ? ArenaEnding.Storm : ArenaEnding.CoreBreached;
                    break;
                case MatchResult.TimeLimit: result.Ending = ArenaEnding.TimeLimit; break;
                case MatchResult.Draw: result.Ending = ArenaEnding.Draw; break;
                default: result.Ending = ArenaEnding.Draw; break;
            }
        }

        // A cheap fingerprint of "any block anywhere changed damage or disappeared".
        static int Signature(ServerSimulation sim, int peerA, int peerB)
        {
            var ships = sim.Ships;
            int h = 17;
            foreach (int peer in new[] { peerA, peerB })
            {
                foreach (var kv in ships[peer].Grid.All) h = unchecked(h * 31 + kv.Key * 7 + kv.Value.Damage);
            }
            return h;
        }

        static float CoreHealth(BlockGrid grid)
        {
            if (!grid.CoreKey.HasValue || !grid.TryGet(grid.CoreKey.Value, out Block core)) return 0f;
            return math.max(0f, 255 - core.Damage) / 255f;
        }
    }
}
