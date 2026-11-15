using System;
using Unity.Mathematics;

namespace Hullbreach.World
{
    // One storm phase: warn (hold), shrink the ring, then the damage rate that applies from the
    // start of this phase's hold until the next phase begins.
    // frob:doc docs/reference/hullbreach-world.md#stormzone
    public readonly struct StormPhase
    {
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float HoldSeconds;
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float ShrinkSeconds;
        // Ring radius when the shrink ends; 0 is a fully closed ring.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float TargetRadius;
        // HP per second dealt to each block outside the ring.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float DamagePerSecond;

        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public StormPhase(float holdSeconds, float shrinkSeconds, float targetRadius, float damagePerSecond)
        {
            HoldSeconds = holdSeconds; ShrinkSeconds = shrinkSeconds;
            TargetRadius = targetRadius; DamagePerSecond = damagePerSecond;
        }
    }

    // Every storm tunable in one place. Default() closes the last ring ~284 s in, well inside
    // the server's 600 s time limit, so the storm only ever decides stalemates.
    // frob:doc docs/reference/hullbreach-world.md#stormzone
    public readonly struct StormConfig
    {
        // Every hold must warn at least this long before a shrink.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public const float MinWarningSeconds = 20f;

        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float TickRate;
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float StartRadius;
        // No storm at all for this long so ships can settle.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float GraceSeconds;
        // Fraction of the radius just inside the edge that warns without damaging.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float WarningFraction;
        // Projectiles/debris past StartRadius + this are despawned.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float HardLimitMargin;
        // Damage after the last ring closes; the only damage that may reach the core.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float SuddenDeathDamagePerSecond;
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly StormPhase[] Phases;

        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public StormConfig(float tickRate, float startRadius, float graceSeconds, float warningFraction,
            float hardLimitMargin, float suddenDeathDps, StormPhase[] phases)
        {
            TickRate = tickRate; StartRadius = startRadius; GraceSeconds = graceSeconds;
            WarningFraction = warningFraction; HardLimitMargin = hardLimitMargin;
            SuddenDeathDamagePerSecond = suddenDeathDps; Phases = phases;
        }

        // 100 -> 70 -> 45 -> 25 -> 10 -> 0, damage doubling each phase; ends at 284 s.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public static StormConfig Default() => new StormConfig(50f, 100f, 30f, 0.1f, 20f, 200f, new[]
        {
            new StormPhase(25f, 30f, 70f, 8f),
            new StormPhase(25f, 30f, 45f, 16f),
            new StormPhase(22f, 30f, 25f, 32f),
            new StormPhase(22f, 25f, 10f, 64f),
            new StormPhase(20f, 25f, 0f, 128f),
        });

        // Total storm length in seconds, including grace.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public float TotalSeconds()
        {
            float t = GraceSeconds;
            if (Phases != null) foreach (StormPhase p in Phases) t += p.HoldSeconds + p.ShrinkSeconds;
            return t;
        }

        // Null when usable, else the first problem found.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public string Validate()
        {
            if (!(TickRate > 0f)) return "TickRate must be > 0";
            if (!(StartRadius > 0f)) return "StartRadius must be > 0";
            if (!(GraceSeconds >= 0f)) return "GraceSeconds must be >= 0";
            if (!(WarningFraction >= 0f && WarningFraction < 1f)) return "WarningFraction must be in [0, 1)";
            if (!(HardLimitMargin >= 0f)) return "HardLimitMargin must be >= 0";
            if (!(SuddenDeathDamagePerSecond >= 0f)) return "SuddenDeathDamagePerSecond must be >= 0";
            if (Phases == null || Phases.Length == 0) return "at least one phase is required";
            float prev = StartRadius;
            for (int i = 0; i < Phases.Length; i++)
            {
                StormPhase p = Phases[i];
                if (!(p.HoldSeconds >= MinWarningSeconds)) return "phase " + i + " hold is under the warning minimum";
                if (!(p.ShrinkSeconds > 0f)) return "phase " + i + " shrink must be > 0";
                if (!(p.TargetRadius >= 0f && p.TargetRadius <= prev)) return "phase " + i + " target must shrink";
                if (!(p.DamagePerSecond >= 0f)) return "phase " + i + " damage must be >= 0";
                prev = p.TargetRadius;
            }
            return null;
        }
    }

    // frob:doc docs/reference/hullbreach-world.md#stormzone
    public enum StormStage : byte { Grace = 0, Hold = 1, Shrink = 2, SuddenDeath = 3 }

    // Snapshot of the storm at one tick: everything the HUD, minimap and damage code ask for.
    // frob:doc docs/reference/hullbreach-world.md#stormzone
    public readonly struct StormState
    {
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly StormStage Stage;
        // Index of the phase being held/shrunk (the next one during Grace); phase count in SuddenDeath.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly int PhaseIndex;
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float2 Center;
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float Radius;
        // Where the ring will be when the upcoming/ongoing shrink ends.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float2 NextCenter;
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float NextRadius;
        // Ticks until the stage changes; -1 in SuddenDeath.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly int TicksUntilNextStage;
        // Ticks until the next shrink starts; 0 while shrinking, -1 when none is left.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly int TicksUntilShrink;
        // HP per tick for a block outside the ring.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float DamagePerTick;
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float WarningFraction;
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float2 HardLimitCenter;
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public readonly float HardLimitRadius;

        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public StormState(StormStage stage, int phaseIndex, float2 center, float radius, float2 nextCenter,
            float nextRadius, int ticksUntilNextStage, int ticksUntilShrink, float damagePerTick,
            float warningFraction, float2 hardCenter, float hardRadius)
        {
            Stage = stage; PhaseIndex = phaseIndex; Center = center; Radius = radius;
            NextCenter = nextCenter; NextRadius = nextRadius; TicksUntilNextStage = ticksUntilNextStage;
            TicksUntilShrink = ticksUntilShrink; DamagePerTick = damagePerTick;
            WarningFraction = warningFraction; HardLimitCenter = hardCenter; HardLimitRadius = hardRadius;
        }

        // Last stage: the ring is closed and the core can take damage.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public bool IsSuddenDeath => Stage == StormStage.SuddenDeath;

        // Display name for the HUD.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public string StageName => Stage.ToString();

        // True on the ring edge or inside; a closed (radius 0) ring contains nothing.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public bool Contains(float2 point) => Radius > 0f && math.length(point - Center) <= Radius;

        // Distance to the edge: positive inside, negative outside.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public float SignedDistance(float2 point) => Radius - math.length(point - Center);

        // Inside but within the no-damage warning band at the edge.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public bool InWarningBand(float2 point)
        {
            if (!Contains(point)) return false;
            return SignedDistance(point) <= Radius * WarningFraction;
        }

        // Unit vector toward the safe centre for an off-screen HUD arrow; zero at the centre.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public float2 PullDirection(float2 point) => math.normalizesafe(Center - point);

        // Projectiles and debris outside this fixed disc are despawned.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public bool WithinHardLimit(float2 point) => math.length(point - HardLimitCenter) <= HardLimitRadius;

        // HP this tick for a block at point; the core is spared until sudden death.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public float DamageFor(float2 point, bool isCore = false)
        {
            if (Contains(point)) return 0f;
            if (isCore && !IsSuddenDeath) return 0f;
            return DamagePerTick;
        }
    }

    // Deterministic storm schedule: a pure function of (config, arena centre, seed, match tick),
    // so server and clients agree without sending anything but the tick (S41).
    // frob:ticket 01M7EXAGAAS0CRAVC1BEXJ5598
    // Grace -> [Hold -> Shrink]* -> SuddenDeath. The ring drifts toward a seed-chosen planet.
    // frob:doc docs/reference/hullbreach-world.md#stormzone
    public sealed class StormZone
    {
        readonly StormConfig _config;
        readonly float2 _arenaCenter;
        readonly int _n;
        // Tick at which phase i's hold starts, its shrink starts, and the shrink ends.
        readonly int[] _holdStart, _shrinkStart, _shrinkEnd;
        // Ring centre/radius at the start (index 0) and end of each phase (index i+1).
        readonly float2[] _centers;
        readonly float[] _radii;

        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public StormConfig Config => _config;
        // The planet (or other candidate) the last ring drifts toward.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public float2 FinalCenter { get; }
        // First tick of sudden death; every shrink is finished by then.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public int SuddenDeathTick { get; }

        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public StormZone(StormConfig config, float2 arenaCenter, float2[] finalCenterCandidates, uint seed)
        {
            string error = config.Validate();
            if (error != null) throw new ArgumentException(error, nameof(config));
            _config = config;
            _arenaCenter = arenaCenter;
            _n = config.Phases.Length;
            FinalCenter = finalCenterCandidates != null && finalCenterCandidates.Length > 0
                ? finalCenterCandidates[(int)(Mix(seed) % (uint)finalCenterCandidates.Length)]
                : arenaCenter;

            _holdStart = new int[_n]; _shrinkStart = new int[_n]; _shrinkEnd = new int[_n];
            _centers = new float2[_n + 1]; _radii = new float[_n + 1];
            _centers[0] = arenaCenter; _radii[0] = config.StartRadius;
            int t = Ticks(config.GraceSeconds);
            for (int i = 0; i < _n; i++)
            {
                StormPhase p = config.Phases[i];
                _holdStart[i] = t;
                t += Ticks(p.HoldSeconds);
                _shrinkStart[i] = t;
                t += Math.Max(1, Ticks(p.ShrinkSeconds));
                _shrinkEnd[i] = t;
                // Even steps toward the target, clamped so each ring stays inside the previous one.
                float2 step = (FinalCenter - _centers[i]) / (_n - i);
                float room = _radii[i] - p.TargetRadius;
                float len = math.length(step);
                if (len > room) step = step / len * room;
                _centers[i + 1] = _centers[i] + step;
                _radii[i + 1] = p.TargetRadius;
            }
            SuddenDeathTick = t;
        }

        int Ticks(float seconds) => (int)(seconds * _config.TickRate + 0.5f);

        // Small integer hash (murmur3 finalizer) so neighbouring seeds pick unrelated planets.
        static uint Mix(uint x)
        {
            unchecked
            {
                x += 0x9E3779B9u;
                x ^= x >> 16; x *= 0x85EBCA6Bu;
                x ^= x >> 13; x *= 0xC2B2AE35u;
                x ^= x >> 16;
                return x;
            }
        }

        // Storm state at a match tick; negative ticks read as tick 0.
        // frob:doc docs/reference/hullbreach-world.md#stormzone
        public StormState At(int tick)
        {
            if (tick < 0) tick = 0;
            float perTick = 1f / _config.TickRate;
            float2 hc = _arenaCenter;
            float hr = _config.StartRadius + _config.HardLimitMargin;
            float warn = _config.WarningFraction;

            if (tick < _holdStart[0])
                return new StormState(StormStage.Grace, 0, _centers[0], _radii[0], _centers[1], _radii[1],
                    _holdStart[0] - tick, _shrinkStart[0] - tick, 0f, warn, hc, hr);

            for (int i = 0; i < _n; i++)
            {
                float rate = _config.Phases[i].DamagePerSecond * perTick;
                if (tick < _shrinkStart[i])
                    return new StormState(StormStage.Hold, i, _centers[i], _radii[i], _centers[i + 1],
                        _radii[i + 1], _shrinkStart[i] - tick, _shrinkStart[i] - tick, rate, warn, hc, hr);
                if (tick < _shrinkEnd[i])
                {
                    float f = (float)(tick - _shrinkStart[i]) / (_shrinkEnd[i] - _shrinkStart[i]);
                    return new StormState(StormStage.Shrink, i,
                        math.lerp(_centers[i], _centers[i + 1], f), math.lerp(_radii[i], _radii[i + 1], f),
                        _centers[i + 1], _radii[i + 1], _shrinkEnd[i] - tick, 0, rate, warn, hc, hr);
                }
            }
            // Closed ring: everyone is outside, so sudden death hurts the core too.
            return new StormState(StormStage.SuddenDeath, _n, _centers[_n], 0f, _centers[_n], 0f, -1, -1,
                _config.SuddenDeathDamagePerSecond * perTick, warn, hc, hr);
        }
    }
}
