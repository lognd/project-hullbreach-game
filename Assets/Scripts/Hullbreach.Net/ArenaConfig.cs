using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Mathematics;
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

        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public ArenaConfig(IReadOnlyList<ArenaBodyConfig> bodies, float lateral, float altitude,
            float vertical, float angleStep)
        {
            Bodies = bodies;
            SpawnLateral = lateral;
            SpawnAltitude = altitude;
            SpawnVertical = vertical;
            SpawnAngleStep = angleStep;
        }

        // The constants ConfigureDemoArena used before they became configurable.
        // frob:doc docs/reference/hullbreach-net.md#arenaconfig
        public static ArenaConfig Defaults() => new ArenaConfig(
            new[]
            {
                new ArenaBodyConfig(0f, -60f, 900f, 18f, 0.2f),
                new ArenaBodyConfig(45f, 20f, 120f, 6f, 0.2f),
            },
            12f, 30f, 2f, 0.18f);
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
            float vertical = d.SpawnVertical, step = d.SpawnAngleStep;
            if (root.TryGetValue("spawn", out object rawSpawn))
            {
                var spawn = rawSpawn as Dictionary<string, object>;
                if (spawn == null) return ConfigResult<ArenaConfig>.Fail("spawn must be an object");
                string err = Read(spawn, "spawn", "lateral", ref lateral)
                    ?? Read(spawn, "spawn", "altitude", ref altitude)
                    ?? Read(spawn, "spawn", "vertical", ref vertical)
                    ?? Read(spawn, "spawn", "angleStep", ref step);
                if (err != null) return ConfigResult<ArenaConfig>.Fail(err);
                if (altitude <= 0f) return ConfigResult<ArenaConfig>.Fail("spawn.altitude must be > 0");
            }
            return ConfigResult<ArenaConfig>.Ok(new ArenaConfig(bodies, lateral, altitude, vertical, step));
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
