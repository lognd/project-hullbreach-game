using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Mathematics;
using Hullbreach.Builder;
using Hullbreach.Platform;
using Hullbreach.World;

namespace Hullbreach.Net
{
    // One gravity body of the arena as authored in config (S40-3).
    // frob:doc docs/reference/hullbreach-net.md#arenaconfig
    public readonly struct ArenaBodyConfig
    {
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public readonly float X;
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public readonly float Y;
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public readonly float Mu;
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public readonly float Radius;
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public readonly float Restitution;

        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public ArenaBodyConfig(float x, float y, float mu, float radius, float restitution)
        {
            X = x; Y = y; Mu = mu; Radius = radius; Restitution = restitution;
        }

        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public GravityBody ToGravityBody() => new GravityBody(new float2(X, Y), Mu, Radius, Restitution);
    }

    // Gravity bodies plus the ring on which ships spawn in orbit around Bodies[0].
    // frob:doc docs/reference/hullbreach-net.md#arenaconfig
    public sealed class ArenaConfig
    {
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public IReadOnlyList<ArenaBodyConfig> Bodies { get; }
        // Sideways swing of the spawn ring, in world units.
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public float SpawnLateral { get; }
        // Distance above the primary body's centre at which ships spawn.
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public float SpawnAltitude { get; }
        // Vertical wobble of the ring, in world units.
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public float SpawnVertical { get; }
        // Angle between consecutive spawns, as a multiple of pi.
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public float SpawnAngleStep { get; }
        // Soft edge of the arena (S41-1); defaults sit inside Quantization.PositionLimit.
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public ArenaBounds Bounds { get; }
        // Shrinking-ring schedule; StormConfig.Default() unless the JSON has a storm block.
        // frob:ticket 01M7EXAGAAS0CRAVC1BEXJ5598
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public StormConfig Storm { get; }
        // Distance between the two match spawns, which sit on opposite sides of Bounds.Center on a horizontal line facing
        // each other, so a fight starts with an approach instead of point-blank range. 0 keeps the legacy orbit ring
        // (SpawnLateral/SpawnAltitude/...) around Bodies[0]. Defaults() uses DefaultSpawnSeparation.
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public float SpawnSeparation { get; }
        // Height of the spawn line above Bounds.Center (negative is below it); keeps both spawns off the moon's side.
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public float SpawnLineY { get; }
        // Planet crash, atmosphere and warning tunables; PlanetCrashConfig.Default() unless the JSON has a planets block.
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public PlanetCrashConfig Crash { get; }
        // Block-economy tunables; EconomyConfig.Default() unless the JSON has an economy block.
        // frob:ticket 01M7EYG4KH9BT32Q4ERZVBQ3M4
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public EconomyConfig Economy { get; }


        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public ArenaConfig(IReadOnlyList<ArenaBodyConfig> bodies, float lateral, float altitude,
            float vertical, float angleStep, ArenaBounds? bounds = null, StormConfig? storm = null,
            PlanetCrashConfig? crash = null, EconomyConfig? economy = null, float spawnSeparation = 0f, float spawnLineY = 0f)
        {
            SpawnSeparation = spawnSeparation;
            SpawnLineY = spawnLineY;
            Bounds = bounds ?? DefaultBounds;
            Storm = storm ?? StormConfig.Default();
            Crash = crash ?? PlanetCrashConfig.Default();
            Economy = economy ?? EconomyConfig.Default();
            Bodies = bodies;
            SpawnLateral = lateral;
            SpawnAltitude = altitude;
            SpawnVertical = vertical;
            SpawnAngleStep = angleStep;
        }

        // Radius 100 keeps the whole arena inside the +-128 wire range.
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public static readonly ArenaBounds DefaultBounds = new ArenaBounds(float2.zero, 100f, 20f, 8f);

        // 130 units: about 12 s of approach for a bot closing at its 8 units/s cap, and both spawns stay more than 80 units
        // from the primary planet, outside its atmosphere and its worst gravity (see ArenaSpawnTests).
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public const float DefaultSpawnSeparation = 130f;

        // 20 units below the arena centre: 45 units from the moon (the centre line passes 28 from it) and 76 from the primary.
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public const float DefaultSpawnLineY = -20f;

        // World position and rotation of match spawn `slot` (0-based) out of `count`: slots are spread along a horizontal line
        // through Bounds.Center, each facing the middle. Only meaningful when SpawnSeparation > 0.
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public void SpawnPose(int slot, int count, out float2 position, out float rotation)
        {
            int n = math.max(1, count);
            float offset = (slot - (n - 1) * 0.5f) * SpawnSeparation;
            position = Bounds.Center + new float2(offset, SpawnLineY);
            // Ship-local forward is +y rotated counter-clockwise by Rotation: -90 degrees faces +x, +90 faces -x.
            rotation = offset <= 0f ? -math.PI * 0.5f : math.PI * 0.5f;
        }

        // The constants ConfigureDemoArena used before they became configurable.
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public static ArenaConfig Defaults() => new ArenaConfig(
            new[]
            {
                new ArenaBodyConfig(0f, -60f, 900f, 18f, 0.2f),
                new ArenaBodyConfig(45f, 20f, 120f, 6f, 0.2f),
            },
            12f, 30f, 2f, 0.18f, spawnSeparation: DefaultSpawnSeparation, spawnLineY: DefaultSpawnLineY);
    }

    // Success value or an error message; the loader never throws on bad input.
    // frob:doc docs/reference/hullbreach-net.md#arenaconfig
    public readonly struct ConfigResult<T>
    {
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public readonly bool IsOk;
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public readonly T Value;
        // Null when IsOk.
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public readonly string Error;

        ConfigResult(bool ok, T value, string error) { IsOk = ok; Value = value; Error = error; }

        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public static ConfigResult<T> Ok(T value) => new ConfigResult<T>(true, value, null);
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public static ConfigResult<T> Fail(string error) => new ConfigResult<T>(false, default, error);
    }

    // Engine-free JSON loader for ArenaConfig: missing keys fall back to Defaults(),
    // bad values come back as errors (S40-3).
    // frob:doc docs/reference/hullbreach-net.md#arenaconfig
    public static class ArenaConfigLoader
    {
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public static ConfigResult<ArenaConfig> Parse(string json)
        {
            if (!PlatformJson.TryParseObject(json, out var root))
                return ConfigResult<ArenaConfig>.Fail("arena config is not a JSON object");
            ArenaConfig d = ArenaConfig.Defaults();

            var bodies = new List<ArenaBodyConfig>(d.Bodies);
            if (root.TryGetValue("bodies", out object rawBodies))
            {
                var list = rawBodies as List<object>;
                if (list == null || list.Count == 0)
                    return ConfigResult<ArenaConfig>.Fail("bodies must be a non-empty array");
                bodies.Clear();
                for (int i = 0; i < list.Count; i++)
                {
                    var r = ParseBody(list[i] as Dictionary<string, object>, i);
                    if (!r.IsOk) return ConfigResult<ArenaConfig>.Fail(r.Error);
                    bodies.Add(r.Value);
                }
            }

            float lateral = d.SpawnLateral, altitude = d.SpawnAltitude;
            float vertical = d.SpawnVertical, step = d.SpawnAngleStep, separation = d.SpawnSeparation, lineY = d.SpawnLineY;
            if (root.TryGetValue("spawn", out object rawSpawn))
            {
                var spawn = rawSpawn as Dictionary<string, object>;
                if (spawn == null) return ConfigResult<ArenaConfig>.Fail("spawn must be an object");
                string err = Read(spawn, "spawn", "lateral", ref lateral)
                    ?? Read(spawn, "spawn", "altitude", ref altitude)
                    ?? Read(spawn, "spawn", "vertical", ref vertical)
                    ?? Read(spawn, "spawn", "angleStep", ref step)
                    ?? Read(spawn, "spawn", "separation", ref separation)
                    ?? Read(spawn, "spawn", "lineY", ref lineY);
                if (err != null) return ConfigResult<ArenaConfig>.Fail(err);
                if (altitude <= 0f) return ConfigResult<ArenaConfig>.Fail("spawn.altitude must be > 0");
                // Authoring the old ring keys without a separation keeps the old ring.
                if (!spawn.ContainsKey("separation") && spawn.Count > 0) separation = 0f;
                if (separation < 0f) return ConfigResult<ArenaConfig>.Fail("spawn.separation must be >= 0");
            }

            ArenaBounds bounds = d.Bounds;
            if (root.TryGetValue("bounds", out object rawBounds))
            {
                var b = rawBounds as Dictionary<string, object>;
                if (b == null) return ConfigResult<ArenaConfig>.Fail("bounds must be an object");
                float bx = bounds.Center.x, by = bounds.Center.y, br = bounds.Radius;
                float bm = bounds.Margin, bs = bounds.Strength;
                string err = Read(b, "bounds", "x", ref bx) ?? Read(b, "bounds", "y", ref by)
                    ?? Read(b, "bounds", "radius", ref br) ?? Read(b, "bounds", "margin", ref bm)
                    ?? Read(b, "bounds", "strength", ref bs);
                if (err == null && br <= 0f) err = "bounds.radius must be > 0";
                if (err == null && bm < 0f) err = "bounds.margin must be >= 0";
                if (err == null && bs < 0f) err = "bounds.strength must be >= 0";
                if (err != null) return ConfigResult<ArenaConfig>.Fail(err);
                bounds = new ArenaBounds(new float2(bx, by), br, bm, bs);
            }

            StormConfig storm = d.Storm;
            if (root.TryGetValue("storm", out object rawStorm))
            {
                var st = ParseStorm(rawStorm as Dictionary<string, object>, storm);
                if (!st.IsOk) return ConfigResult<ArenaConfig>.Fail(st.Error);
                storm = st.Value;
            }

            PlanetCrashConfig crash = d.Crash;
            if (root.TryGetValue("planets", out object rawPlanets))
            {
                var pl = ParsePlanets(rawPlanets as Dictionary<string, object>, crash);
                if (!pl.IsOk) return ConfigResult<ArenaConfig>.Fail(pl.Error);
                crash = pl.Value;
            }
            EconomyConfig economy = d.Economy;
            if (root.TryGetValue("economy", out object rawEconomy))
            {
                var ec = ParseEconomy(rawEconomy as Dictionary<string, object>, economy);
                if (!ec.IsOk) return ConfigResult<ArenaConfig>.Fail(ec.Error);
                economy = ec.Value;
            }
            return ConfigResult<ArenaConfig>.Ok(new ArenaConfig(bodies, lateral, altitude, vertical, step, bounds, storm, crash, economy, separation, lineY));
        }

        // Reads a file and parses it; an unreadable file is an error, not an exception.
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public static ConfigResult<ArenaConfig> LoadFile(string path)
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                return ConfigResult<ArenaConfig>.Fail("cannot read arena config '" + path + "': " + e.Message);
            }
            return Parse(text);
        }

        // Planets block: every key is an optional float override of PlanetCrashConfig, then Validate has the last word.
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        static ConfigResult<PlanetCrashConfig> ParsePlanets(Dictionary<string, object> o, PlanetCrashConfig d)
        {
            if (o == null) return ConfigResult<PlanetCrashConfig>.Fail("planets must be an object");
            PlanetCrashConfig c = d;
            float substeps = c.MaxSubsteps;
            string err = Read(o, "planets", "safeLandingSpeed", ref c.SafeLandingSpeed)
                ?? Read(o, "planets", "catastrophicSpeed", ref c.CatastrophicSpeed)
                ?? Read(o, "planets", "damagePerSpeed", ref c.DamagePerSpeed)
                ?? Read(o, "planets", "coreDamageScale", ref c.CoreDamageScale)
                ?? Read(o, "planets", "coreMaxDamagePerImpact", ref c.CoreMaxDamagePerImpact)
                ?? Read(o, "planets", "restSpeed", ref c.RestSpeed)
                ?? Read(o, "planets", "maxRestitution", ref c.MaxRestitution)
                ?? Read(o, "planets", "sweepStep", ref c.SweepStep)
                ?? Read(o, "planets", "maxSubsteps", ref substeps)
                ?? Read(o, "planets", "atmosphereDepthFactor", ref c.AtmosphereDepthFactor)
                ?? Read(o, "planets", "atmosphereMinDepth", ref c.AtmosphereMinDepth)
                ?? Read(o, "planets", "atmosphereSafeSpeed", ref c.AtmosphereSafeSpeed)
                ?? Read(o, "planets", "atmosphereDrag", ref c.AtmosphereDrag)
                ?? Read(o, "planets", "heatSpeed", ref c.HeatSpeed)
                ?? Read(o, "planets", "heatDamagePerSecond", ref c.HeatDamagePerSecond)
                ?? Read(o, "planets", "heatMaxDamagePerSecond", ref c.HeatMaxDamagePerSecond)
                ?? Read(o, "planets", "warningSeconds", ref c.WarningSeconds);
            if (err != null) return ConfigResult<PlanetCrashConfig>.Fail(err);
            c.MaxSubsteps = (int)Math.Round(substeps);
            string problem = c.Validate();
            return problem == null
                ? ConfigResult<PlanetCrashConfig>.Ok(c)
                : ConfigResult<PlanetCrashConfig>.Fail("planets: " + problem);
        }

        // Storm block: scalar keys override the defaults, a phases array replaces the default list,
        // and StormConfig.Validate has the last word.
        // frob:ticket 01M7EXAGAAS0CRAVC1BEXJ5598
        static ConfigResult<StormConfig> ParseStorm(Dictionary<string, object> o, StormConfig d)
        {
            if (o == null) return ConfigResult<StormConfig>.Fail("storm must be an object");
            float tick = d.TickRate, radius = d.StartRadius, grace = d.GraceSeconds, warn = d.WarningFraction;
            float margin = d.HardLimitMargin, sudden = d.SuddenDeathDamagePerSecond;
            string err = Read(o, "storm", "tickRate", ref tick) ?? Read(o, "storm", "startRadius", ref radius)
                ?? Read(o, "storm", "graceSeconds", ref grace) ?? Read(o, "storm", "warningFraction", ref warn)
                ?? Read(o, "storm", "hardLimitMargin", ref margin)
                ?? Read(o, "storm", "suddenDeathDamagePerSecond", ref sudden);
            if (err != null) return ConfigResult<StormConfig>.Fail(err);
            StormPhase[] phases = d.Phases;
            if (o.TryGetValue("phases", out object rawPhases))
            {
                var list = rawPhases as List<object>;
                if (list == null || list.Count == 0)
                    return ConfigResult<StormConfig>.Fail("storm.phases must be a non-empty array");
                phases = new StormPhase[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    string at = "storm.phases[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                    var p = list[i] as Dictionary<string, object>;
                    if (p == null) return ConfigResult<StormConfig>.Fail(at + " must be an object");
                    float hold = 0f, shrink = 0f, target = 0f, dps = 0f;
                    foreach (string required in new[] { "hold", "shrink", "radius", "damage" })
                        if (!p.ContainsKey(required)) return ConfigResult<StormConfig>.Fail(at + "." + required + " is required");
                    err = Read(p, at, "hold", ref hold) ?? Read(p, at, "shrink", ref shrink)
                        ?? Read(p, at, "radius", ref target) ?? Read(p, at, "damage", ref dps);
                    if (err != null) return ConfigResult<StormConfig>.Fail(err);
                    phases[i] = new StormPhase(hold, shrink, target, dps);
                }
            }
            var config = new StormConfig(tick, radius, grace, warn, margin, sudden, phases);
            string problem = config.Validate();
            return problem == null
                ? ConfigResult<StormConfig>.Ok(config)
                : ConfigResult<StormConfig>.Fail("storm: " + problem);
        }

        static readonly string[] TypeKeys = { "core", "hull", "armor", "thruster", "cannon", "fin", "retroThruster" };

        // Economy block: scalar keys override the defaults; "costs" and "startingStock" are objects keyed by
        // block name (hull, armor, thruster, cannon, fin, retroThruster); EconomyConfig.Validate has the last word.
        // frob:ticket 01M7EYG4KH9BT32Q4ERZVBQ3M4
        static ConfigResult<EconomyConfig> ParseEconomy(Dictionary<string, object> o, EconomyConfig d)
        {
            if (o == null) return ConfigResult<EconomyConfig>.Fail("economy must be an object");
            bool enabled = d.Enabled;
            if (o.TryGetValue("enabled", out object rawEnabled))
            {
                if (!(rawEnabled is bool b)) return ConfigResult<EconomyConfig>.Fail("economy.enabled must be true or false");
                enabled = b;
            }
            float budget = d.Costs.Budget, maxBlocks = d.Costs.MaxBlocks, weld = d.Costs.WeldedSurcharge;
            float maxStock = d.MaxStockPerType, conSec = d.ConstructionSeconds, conDmg = d.ConstructionDamage;
            float salvage = d.SalvagePercent, spread = d.SalvageSpread, refund = d.RefundPercent;
            float trickle = d.TrickleSeconds, tStep = d.TrickleDeficitStep, tMax = d.TrickleMaxMultiplier;
            float tStock = d.TrickleStockCap, tMatch = d.TrickleMatchCap;
            string err = Read(o, "economy", "budget", ref budget) ?? Read(o, "economy", "maxBlocks", ref maxBlocks)
                ?? Read(o, "economy", "weldedSurcharge", ref weld) ?? Read(o, "economy", "maxStockPerType", ref maxStock)
                ?? Read(o, "economy", "constructionSeconds", ref conSec) ?? Read(o, "economy", "constructionDamage", ref conDmg)
                ?? Read(o, "economy", "salvagePercent", ref salvage) ?? Read(o, "economy", "salvageSpread", ref spread)
                ?? Read(o, "economy", "refundPercent", ref refund) ?? Read(o, "economy", "trickleSeconds", ref trickle)
                ?? Read(o, "economy", "trickleDeficitStep", ref tStep) ?? Read(o, "economy", "trickleMaxMultiplier", ref tMax)
                ?? Read(o, "economy", "trickleStockCap", ref tStock) ?? Read(o, "economy", "trickleMatchCap", ref tMatch);
            if (err != null) return ConfigResult<EconomyConfig>.Fail(err);
            if (conDmg < 0f || conDmg > 254f) return ConfigResult<EconomyConfig>.Fail("economy.constructionDamage must be within 0..254");

            BlockCosts costs = d.Costs.WithLimits((int)budget, (int)maxBlocks, (int)weld);
            int[] stock = (int[])d.StartingStock.Clone();
            string tableErr = ReadTable(o, "costs", ref costs, stock, false)
                ?? ReadTable(o, "startingStock", ref costs, stock, true);
            if (tableErr != null) return ConfigResult<EconomyConfig>.Fail(tableErr);

            var pickups = d.Pickups;
            if (o.TryGetValue("pickups", out object rawPickups))
            {
                var po = rawPickups as Dictionary<string, object>;
                if (po == null) return ConfigResult<EconomyConfig>.Fail("economy.pickups must be an object");
                float mag = pickups.MagnetRadius, rel = pickups.MaxRelativeSpeed, first = pickups.CrateFirstSeconds;
                float every = pickups.CrateIntervalSeconds, life = pickups.CrateLifetimeSeconds, drift = pickups.CrateDriftSpeed;
                float salLife = pickups.SalvageLifetimeSeconds, salMax = pickups.MaxSalvage, arm = pickups.ArmSeconds;
                err = Read(po, "economy.pickups", "magnetRadius", ref mag) ?? Read(po, "economy.pickups", "maxRelativeSpeed", ref rel)
                    ?? Read(po, "economy.pickups", "crateFirstSeconds", ref first) ?? Read(po, "economy.pickups", "crateIntervalSeconds", ref every)
                    ?? Read(po, "economy.pickups", "crateLifetimeSeconds", ref life) ?? Read(po, "economy.pickups", "crateDriftSpeed", ref drift)
                    ?? Read(po, "economy.pickups", "salvageLifetimeSeconds", ref salLife) ?? Read(po, "economy.pickups", "maxSalvage", ref salMax) ?? Read(po, "economy.pickups", "armSeconds", ref arm);
                if (err != null) return ConfigResult<EconomyConfig>.Fail(err);
                pickups = new PickupConfig(mag, rel, first, every, life, drift, salLife, (int)salMax, arm);
            }

            var config = new EconomyConfig(enabled, costs, stock, (int)maxStock, conSec, (byte)conDmg, (int)salvage, spread,
                (int)refund, trickle, tStep, tMax, (int)tStock, (int)tMatch, pickups);
            string problem = config.Validate();
            return problem == null
                ? ConfigResult<EconomyConfig>.Ok(config)
                : ConfigResult<EconomyConfig>.Fail("economy: " + problem);
        }

        // Reads economy.costs (stock == false) or economy.startingStock (stock == true) in place.
        static string ReadTable(Dictionary<string, object> o, string name, ref BlockCosts costs,
            int[] stock, bool isStock)
        {
            if (!o.TryGetValue(name, out object raw)) return null;
            var table = raw as Dictionary<string, object>;
            if (table == null) return "economy." + name + " must be an object";
            foreach (var kv in table)
            {
                int type = System.Array.IndexOf(TypeKeys, kv.Key);
                if (type < 0) return "economy." + name + "." + kv.Key + " is not a block name";
                float value = 0f;
                string err = Read(table, "economy." + name, kv.Key, ref value);
                if (err != null) return err;
                if (value < 0f || value != System.Math.Floor(value)) return "economy." + name + "." + kv.Key + " must be a whole number >= 0";
                if (isStock) stock[type] = (int)value; else costs = costs.WithCost((byte)type, (int)value);
            }
            return null;
        }

        static ConfigResult<ArenaBodyConfig> ParseBody(Dictionary<string, object> o, int index)
        {
            string at = "bodies[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            if (o == null) return ConfigResult<ArenaBodyConfig>.Fail(at + " must be an object");
            float x = 0f, y = 0f, mu = 0f, radius = 0f, rest = 0.2f;
            if (!o.ContainsKey("mu")) return ConfigResult<ArenaBodyConfig>.Fail(at + ".mu is required");
            if (!o.ContainsKey("radius")) return ConfigResult<ArenaBodyConfig>.Fail(at + ".radius is required");
            string err = Read(o, at, "x", ref x) ?? Read(o, at, "y", ref y) ?? Read(o, at, "mu", ref mu)
                ?? Read(o, at, "radius", ref radius) ?? Read(o, at, "restitution", ref rest);
            if (err == null && mu <= 0f) err = at + ".mu must be > 0";
            if (err == null && radius <= 0f) err = at + ".radius must be > 0";
            if (err == null && (rest < 0f || rest > 1f)) err = at + ".restitution must be within [0, 1]";
            return err != null
                ? ConfigResult<ArenaBodyConfig>.Fail(err)
                : ConfigResult<ArenaBodyConfig>.Ok(new ArenaBodyConfig(x, y, mu, radius, rest));
        }

        // Leaves target untouched when the key is absent; returns an error text or null.
        static string Read(Dictionary<string, object> o, string at, string key, ref float target)
        {
            if (!o.TryGetValue(key, out object raw)) return null;
            if (!(raw is double d) || double.IsNaN(d) || double.IsInfinity(d) || Math.Abs(d) > float.MaxValue)
                return at + "." + key + " must be a finite number";
            target = (float)d;
            return null;
        }
    }
}
