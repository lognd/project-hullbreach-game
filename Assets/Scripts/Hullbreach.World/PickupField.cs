using System;
using System.Collections.Generic;
using Hullbreach.Core;
using Unity.Mathematics;

namespace Hullbreach.World
{
    // Where a pickup came from.
    // frob:ticket 01M7EYG4KH9BT32Q4ERZVBQ3M4
    // frob:doc docs/reference/hullbreach-world.md#pickupfield
    public enum PickupKind : byte
    {
        // Debris of a destroyed block; drifts with the velocity it was shot off at.
        Salvage = 0,
        // Seeded supply crate.
        Crate = 1,
    }

    // Why a pickup left the field.
    // frob:doc docs/reference/hullbreach-world.md#pickupfield
    public enum PickupRemoval : byte
    {
        Collected = 0,
        Expired = 1,
        // Drifted into a planet.
        Planet = 2,
        // Drifted past the storm hard limit.
        Storm = 3,
        // Pushed out by a newer salvage when the field was full.
        Culled = 4,
    }

    // One drifting pickup: a count of one block type. Position is a closed-form function of the tick
    // (constant velocity), so a client needs only the spawn message to place it.
    // frob:doc docs/reference/hullbreach-world.md#pickupfield
    public readonly struct Pickup
    {
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly uint Id;
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly PickupKind Kind;
        // A BlockTypes id; never the core.
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly byte Type;
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly byte Count;
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly float2 Origin;
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly float2 Velocity;
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly int SpawnTick;
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly int ExpireTick;

        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public Pickup(uint id, PickupKind kind, byte type, byte count, float2 origin, float2 velocity, int spawnTick, int expireTick)
        {
            Id = id; Kind = kind; Type = type; Count = count; Origin = origin; Velocity = velocity;
            SpawnTick = spawnTick; ExpireTick = expireTick;
        }

        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public float2 PositionAt(int tick, float tickRate) => Origin + Velocity * ((tick - SpawnTick) / tickRate);
    }

    // A pickup appearing or leaving, for the server to broadcast.
    // frob:doc docs/reference/hullbreach-world.md#pickupfield
    public readonly struct PickupEvent
    {
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly bool Spawned;
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly Pickup Pickup;
        // Meaningful when !Spawned.
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly PickupRemoval Reason;
        // Peer that collected it, else 0.
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly int Collector;

        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public PickupEvent(bool spawned, Pickup pickup, PickupRemoval reason, int collector)
        { Spawned = spawned; Pickup = pickup; Reason = reason; Collector = collector; }
    }

    // Every pickup tunable: reach, crate schedule, salvage limits.
    // frob:doc docs/reference/hullbreach-world.md#pickupfield
    public readonly struct PickupConfig
    {
        // Distance from any block centre at which a pickup is grabbed.
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly float MagnetRadius;
        // A ship faster than this relative to the pickup rams straight through it.
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly float MaxRelativeSpeed;
        // Seconds from match start to the first crate wave; 0 or less turns crates off.
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly float CrateFirstSeconds;
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly float CrateIntervalSeconds;
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly float CrateLifetimeSeconds;
        // Crate drift speed in world units per second.
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly float CrateDriftSpeed;
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly float SalvageLifetimeSeconds;
        // The oldest salvage is culled past this many, so a mass collapse cannot flood the field.
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly int MaxSalvage;
        // A new pickup cannot be grabbed for this long, so a ship does not instantly re-take its own wreckage
        // and a rival gets a chance to scramble for it.
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public readonly float ArmSeconds;

        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public PickupConfig(float magnetRadius, float maxRelativeSpeed, float crateFirstSeconds, float crateIntervalSeconds,
            float crateLifetimeSeconds, float crateDriftSpeed, float salvageLifetimeSeconds, int maxSalvage, float armSeconds = 1.5f)
        {
            ArmSeconds = armSeconds;
            MagnetRadius = magnetRadius; MaxRelativeSpeed = maxRelativeSpeed;
            CrateFirstSeconds = crateFirstSeconds; CrateIntervalSeconds = crateIntervalSeconds;
            CrateLifetimeSeconds = crateLifetimeSeconds; CrateDriftSpeed = crateDriftSpeed;
            SalvageLifetimeSeconds = salvageLifetimeSeconds; MaxSalvage = maxSalvage;
        }

        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public static PickupConfig Default() => new PickupConfig(2f, 4f, 20f, 12f, 75f, 1f, 40f, 96, 1.5f);

        // Null when valid, else the first problem.
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public string Validate()
        {
            if (!(MagnetRadius > 0f)) return "magnetRadius must be > 0";
            if (!(MaxRelativeSpeed > 0f)) return "maxRelativeSpeed must be > 0";
            if (CrateFirstSeconds > 0f && !(CrateIntervalSeconds >= 1f)) return "crateIntervalSeconds must be >= 1";
            if (!(CrateLifetimeSeconds > 0f)) return "crateLifetimeSeconds must be > 0";
            if (CrateDriftSpeed < 0f) return "crateDriftSpeed must be >= 0";
            if (!(SalvageLifetimeSeconds > 0f)) return "salvageLifetimeSeconds must be > 0";
            if (MaxSalvage < 1 || MaxSalvage > 1024) return "maxSalvage must be within 1..1024";
            if (ArmSeconds < 0f) return "armSeconds must be >= 0";
            return null;
        }
    }

    // The drifting pickups of one match. Crates are a pure function of (seed, tick, arena), so a replay or a
    // second machine rebuilds the same ones; salvage is added by the server when blocks die. Pure engine-free
    // logic: the server owns collection decisions and calls Reach / Remove.
    // frob:ticket 01M7EYG4KH9BT32Q4ERZVBQ3M4
    // frob:doc docs/reference/hullbreach-world.md#pickupfield
    public sealed class PickupField
    {
        // Crate ids live above this so they never meet salvage ids (a counter from 1).
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public const uint CrateIdBase = 0x40000000u;

        readonly PickupConfig _config;
        readonly uint _seed;
        readonly float _tickRate;
        readonly int _startTick;
        readonly float2 _arenaCenter;
        readonly float _arenaRadius;
        readonly GravityField _gravity;
        readonly StormZone _storm;
        readonly int _stormStartTick;
        readonly List<Pickup> _active = new List<Pickup>();
        uint _nextSalvageId = 1;
        int _nextCrateEvent;

        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public PickupField(PickupConfig config, uint seed, float tickRate, int startTick, float2 arenaCenter,
            float arenaRadius, GravityField gravity, StormZone storm, int stormStartTick)
        {
            _config = config; _seed = seed; _tickRate = tickRate; _startTick = startTick;
            _arenaCenter = arenaCenter; _arenaRadius = arenaRadius; _gravity = gravity;
            _storm = storm; _stormStartTick = stormStartTick;
        }

        // Live pickups in id order.
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public IReadOnlyList<Pickup> Active => _active;

        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public PickupConfig Config => _config;

        // Small integer hash so neighbouring seeds and indices give unrelated numbers.
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public static uint Hash(uint a, uint b, uint c)
        {
            unchecked
            {
                uint x = a * 0x9E3779B1u ^ (b + 0x7F4A7C15u) * 0x85EBCA6Bu ^ (c + 0x165667B1u) * 0xC2B2AE35u;
                x ^= x >> 16; x *= 0x85EBCA6Bu;
                x ^= x >> 13; x *= 0xC2B2AE35u;
                x ^= x >> 16;
                return x;
            }
        }

        static float Unit(uint h) => (h & 0xFFFFFFu) / (float)0x1000000;

        int Ticks(float seconds) => (int)(seconds * _tickRate + 0.5f);

        // Advances to `tick`: spawns due crate waves, then removes expired / planet-eaten / out-of-storm pickups.
        // Events are appended in a deterministic order.
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public void Tick(int tick, List<PickupEvent> events)
        {
            if (_config.CrateFirstSeconds > 0f)
            {
                while (CrateEventTick(_nextCrateEvent) <= tick)
                {
                    var spawned = new List<Pickup>();
                    CrateWave(_nextCrateEvent, spawned);
                    foreach (Pickup p in spawned) Add(p, events);
                    _nextCrateEvent++;
                }
            }

            StormState? zone = _storm != null ? _storm.At(tick - _stormStartTick) : (StormState?)null;
            for (int i = 0; i < _active.Count; i++)
            {
                Pickup p = _active[i];
                float2 pos = p.PositionAt(tick, _tickRate);
                PickupRemoval? why = null;
                if (tick >= p.ExpireTick) why = PickupRemoval.Expired;
                else if (_gravity != null && _gravity.TryContact(pos, 0.5f, out _, out _, out _)) why = PickupRemoval.Planet;
                else if (zone.HasValue && !zone.Value.WithinHardLimit(pos)) why = PickupRemoval.Storm;
                if (!why.HasValue) continue;
                _active.RemoveAt(i--);
                events?.Add(new PickupEvent(false, p, why.Value, 0));
            }
        }

        // The tick crate wave `index` appears on.
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public int CrateEventTick(int index) =>
            _startTick + Ticks(_config.CrateFirstSeconds) + index * Math.Max(1, Ticks(_config.CrateIntervalSeconds));

        // Wave `index` as a pure function of (seed, index, arena, storm): planet wave -> one crate beside a planet;
        // edge wave -> two crates on opposite sides of the storm ring (the rare ones); field wave -> a mirrored pair.
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public void CrateWave(int index, List<Pickup> output)
        {
            int tick = CrateEventTick(index);
            uint h = Hash(_seed, (uint)index, 1u);
            float angle = Unit(h) * 2f * math.PI;
            float2 dir = new float2(math.cos(angle), math.sin(angle));
            float drift = _config.CrateDriftSpeed;
            int expire = tick + Ticks(_config.CrateLifetimeSeconds);
            uint id = CrateIdBase + (uint)index * 4u;
            float2 tangent = new float2(-dir.y, dir.x);

            switch (index % 3)
            {
                case 0:
                {
                    int planets = CountPlanets();
                    if (planets == 0) goto default;
                    _gravity.TryGetPermanent((int)(Hash(_seed, (uint)index, 2u) % (uint)planets), out GravityBody body);
                    float2 pos = body.Position + dir * (body.Radius + 3.5f);
                    float2 vel = tangent * drift * (0.5f + 0.5f * Unit(Hash(_seed, (uint)index, 3u)));
                    Roll(false, Hash(_seed, (uint)index, 4u), out byte type, out byte count);
                    output.Add(new Pickup(id, PickupKind.Crate, type, count, pos, vel, tick, expire));
                    break;
                }
                case 1:
                {
                    float2 center = _arenaCenter;
                    float radius = _arenaRadius * 0.6f;
                    if (_storm != null)
                    {
                        StormState z = _storm.At(tick - _stormStartTick);
                        center = z.Center;
                        radius = math.max(3f, z.Radius - 0.5f);
                    }
                    Roll(true, Hash(_seed, (uint)index, 4u), out byte type, out byte count);
                    output.Add(new Pickup(id, PickupKind.Crate, type, count, center + dir * radius, tangent * drift * 0.5f, tick, expire));
                    output.Add(new Pickup(id + 1, PickupKind.Crate, type, count, center - dir * radius, -tangent * drift * 0.5f, tick, expire));
                    break;
                }
                default:
                {
                    float maxR = _arenaRadius * 0.75f;
                    if (_storm != null) maxR = math.min(maxR, _storm.At(tick - _stormStartTick).Radius * 0.8f);
                    float r = maxR * (0.25f + 0.75f * Unit(Hash(_seed, (uint)index, 5u)));
                    float2 a = _arenaCenter + dir * r;
                    float2 vel = tangent * drift * (0.4f + 0.6f * Unit(Hash(_seed, (uint)index, 3u)));
                    Roll(false, Hash(_seed, (uint)index, 4u), out byte type, out byte count);
                    output.Add(new Pickup(id, PickupKind.Crate, type, count, PushOutOfPlanets(a), vel, tick, expire));
                    output.Add(new Pickup(id + 1, PickupKind.Crate, type, count,
                        PushOutOfPlanets(_arenaCenter - dir * r), -vel, tick, expire));
                    break;
                }
            }
        }

        int CountPlanets()
        {
            if (_gravity == null) return 0;
            int n = 0;
            while (_gravity.TryGetPermanent(n, out _)) n++;
            return n;
        }

        float2 PushOutOfPlanets(float2 p)
        {
            int n = CountPlanets();
            for (int i = 0; i < n; i++)
            {
                _gravity.TryGetPermanent(i, out GravityBody b);
                float2 d = p - b.Position;
                float len = math.length(d);
                float min = b.Radius + 3f;
                if (len < min) p = b.Position + (len > 1e-4f ? d / len : new float2(1f, 0f)) * min;
            }
            return p;
        }

        // Common crates are cheap hull; the storm-edge ones carry the rare blocks (cannon, thruster).
        static void Roll(bool rare, uint h, out byte type, out byte count)
        {
            uint pick = h % 100u;
            uint amount = (h >> 8) % 100u;
            if (rare)
            {
                if (pick < 30) { type = BlockTypes.Cannon; count = 1; }
                else if (pick < 60) { type = BlockTypes.Thruster; count = (byte)(amount < 50 ? 1 : 2); }
                else if (pick < 85) { type = BlockTypes.Armor; count = 3; }
                else { type = BlockTypes.RetroThruster; count = 1; }
                return;
            }
            if (pick < 40) { type = BlockTypes.Hull; count = (byte)(amount < 50 ? 2 : 3); }
            else if (pick < 65) { type = BlockTypes.Armor; count = (byte)(amount < 60 ? 1 : 2); }
            else if (pick < 80) { type = BlockTypes.Thruster; count = 1; }
            else if (pick < 90) { type = BlockTypes.Fin; count = 1; }
            else { type = BlockTypes.RetroThruster; count = 1; }
        }

        // Drops a block of debris. Returns the pickup (the field may also cull its oldest salvage).
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public Pickup AddSalvage(int tick, byte type, float2 position, float2 velocity, List<PickupEvent> events)
        {
            int salvage = 0;
            foreach (Pickup q in _active) if (q.Kind == PickupKind.Salvage) salvage++;
            for (int i = 0; i < _active.Count && salvage >= _config.MaxSalvage; i++)
            {
                if (_active[i].Kind != PickupKind.Salvage) continue;
                Pickup old = _active[i];
                _active.RemoveAt(i--);
                salvage--;
                events?.Add(new PickupEvent(false, old, PickupRemoval.Culled, 0));
            }
            var p = new Pickup(_nextSalvageId++, PickupKind.Salvage, type, 1, position, velocity, tick,
                tick + Ticks(_config.SalvageLifetimeSeconds));
            Add(p, events);
            return p;
        }

        void Add(Pickup p, List<PickupEvent> events)
        {
            int at = _active.Count;
            while (at > 0 && _active[at - 1].Id > p.Id) at--;
            _active.Insert(at, p);
            events?.Add(new PickupEvent(true, p, PickupRemoval.Collected, 0));
        }

        // Takes a pickup out of the field (collection). False when the id is not live, so a double
        // collect can never pay out twice.
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public bool Remove(uint id, PickupRemoval reason, int collector, List<PickupEvent> events)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Id != id) continue;
                Pickup p = _active[i];
                _active.RemoveAt(i);
                events?.Add(new PickupEvent(false, p, reason, collector));
                return true;
            }
            return false;
        }

        // Distance from the nearest of `points` (block centres, world space) to the pickup, or
        // float.PositiveInfinity when it cannot be grabbed: out of the magnet radius, the ship is moving too
        // fast relative to it, or a planet sits between (no collecting through walls).
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public float Reach(in Pickup pickup, int tick, IReadOnlyList<float2> points, float2 shipVelocity)
        {
            if (tick < pickup.SpawnTick + Ticks(_config.ArmSeconds)) return float.PositiveInfinity;
            if (math.length(shipVelocity - pickup.Velocity) > _config.MaxRelativeSpeed) return float.PositiveInfinity;
            float2 at = pickup.PositionAt(tick, _tickRate);
            float best = float.PositiveInfinity;
            float2 from = at;
            for (int i = 0; i < points.Count; i++)
            {
                float d = math.length(points[i] - at);
                if (d < best) { best = d; from = points[i]; }
            }
            if (best > _config.MagnetRadius) return float.PositiveInfinity;
            return SegmentHitsPlanet(from, at) ? float.PositiveInfinity : best;
        }

        // True when the segment a-b passes through a planet disc.
        // frob:doc docs/reference/hullbreach-world.md#pickupfield
        public bool SegmentHitsPlanet(float2 a, float2 b)
        {
            int n = CountPlanets();
            float2 ab = b - a;
            float len2 = math.dot(ab, ab);
            for (int i = 0; i < n; i++)
            {
                _gravity.TryGetPermanent(i, out GravityBody body);
                float t = len2 > 1e-8f ? math.clamp(math.dot(body.Position - a, ab) / len2, 0f, 1f) : 0f;
                if (math.length(a + ab * t - body.Position) < body.Radius) return true;
            }
            return false;
        }
    }
}
