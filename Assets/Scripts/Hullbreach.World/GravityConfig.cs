using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Mathematics;

namespace Hullbreach.World
{
    // One planet's physical constants; visuals stay in the scene.
    // frob:doc docs/reference/hullbreach-world.md#gravityconfig
    public readonly struct PlanetConfig
    {
        // frob:doc docs/reference/hullbreach-world.md#gravityconfig
        public readonly float2 Position;

        // frob:doc docs/reference/hullbreach-world.md#gravityconfig
        public readonly float Mu;

        // frob:doc docs/reference/hullbreach-world.md#gravityconfig
        public readonly float Radius;

        // Zero or less means "use GravityBody.DefaultSoftRadiusFactor".
        // frob:doc docs/reference/hullbreach-world.md#gravityconfig
        public readonly float SoftRadiusFactor;

        // frob:doc docs/reference/hullbreach-world.md#gravityconfig
        public PlanetConfig(float2 position, float mu, float radius, float softRadiusFactor = 0f)
        {
            Position = position;
            Mu = mu;
            Radius = radius;
            SoftRadiusFactor = softRadiusFactor;
        }

        // Same soft-radius rule the client and server both need, so it lives here once.
        // frob:doc docs/reference/hullbreach-world.md#gravityconfig
        public GravityBody ToBody(float surfaceRestitution)
        {
            float factor = SoftRadiusFactor > 0f ? SoftRadiusFactor : GravityBody.DefaultSoftRadiusFactor;
            float softRadius = math.max(Radius * factor, GravityBody.MinSoftRadius);
            return new GravityBody(Position, Mu, Radius, surfaceRestitution, softRadius);
        }
    }

    // Engine-free gravity constants shared by the client GravityWorld and the server.
    // frob:doc docs/reference/hullbreach-world.md#gravityconfig
    public sealed class GravityConfig
    {
        // frob:doc docs/reference/hullbreach-world.md#gravityconfig
        public const float DefaultSurfaceRestitution = 0.2f;

        // frob:doc docs/reference/hullbreach-world.md#gravityconfig
        public static readonly GravityConfig Default = new GravityConfig(
            GravityField.DefaultMaxAcceleration, DefaultSurfaceRestitution, Array.Empty<PlanetConfig>());

        // frob:doc docs/reference/hullbreach-world.md#gravityconfig
        public readonly float MaxAcceleration;

        // frob:doc docs/reference/hullbreach-world.md#gravityconfig
        public readonly float SurfaceRestitution;

        // frob:doc docs/reference/hullbreach-world.md#gravityconfig
        public readonly IReadOnlyList<PlanetConfig> Planets;

        // frob:doc docs/reference/hullbreach-world.md#gravityconfig
        public GravityConfig(float maxAcceleration, float surfaceRestitution, IReadOnlyList<PlanetConfig> planets)
        {
            MaxAcceleration = maxAcceleration;
            SurfaceRestitution = surfaceRestitution;
            Planets = planets;
        }

        // Insertion order is the planets' order in the text, which is also
        // GravityField's permanent-body index order on both sides.
        // frob:doc docs/reference/hullbreach-world.md#gravityconfig
        public GravityField BuildField()
        {
            var field = new GravityField(MaxAcceleration);
            for (int i = 0; i < Planets.Count; i++) field.Add(Planets[i].ToBody(SurfaceRestitution));
            return field;
        }

        // Errors carry the 1-based line number; `config` is null on failure.
        // frob:doc docs/reference/hullbreach-world.md#gravityconfig
        public static bool TryParse(string text, out GravityConfig config, out string error)
        {
            config = null;
            float maxAcceleration = GravityField.DefaultMaxAcceleration;
            float restitution = DefaultSurfaceRestitution;
            var planets = new List<PlanetConfig>();

            string[] lines = (text ?? string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash);
                line = line.Trim();
                if (line.Length == 0) continue;

                int eq = line.IndexOf('=');
                if (eq < 0) { error = Fail(i, "expected 'key = value'"); return false; }
                string key = line.Substring(0, eq).Trim();
                string[] values = line.Substring(eq + 1).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                switch (key)
                {
                    case "max_acceleration":
                        if (!TryOne(values, out maxAcceleration) || !(maxAcceleration > 0f))
                        { error = Fail(i, "max_acceleration needs one number greater than 0"); return false; }
                        break;
                    case "surface_restitution":
                        if (!TryOne(values, out restitution) || restitution < 0f || restitution > 1f)
                        { error = Fail(i, "surface_restitution needs one number from 0 to 1"); return false; }
                        break;
                    case "planet":
                        if (!TryPlanet(values, out var planet))
                        { error = Fail(i, "planet needs 'x y mu radius [soft_radius_factor]' with mu and radius greater than 0"); return false; }
                        planets.Add(planet);
                        break;
                    default:
                        error = Fail(i, "unknown key '" + key + "'");
                        return false;
                }
            }

            config = new GravityConfig(maxAcceleration, restitution, planets);
            error = null;
            return true;
        }

        // For the headless server; the client reads the same file as a TextAsset.
        // frob:doc docs/reference/hullbreach-world.md#gravityconfig
        public static bool TryLoad(string path, out GravityConfig config, out string error)
        {
            config = null;
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = path + ": " + e.Message;
                return false;
            }
            if (TryParse(text, out config, out string parseError)) { error = null; return true; }
            error = path + ": " + parseError;
            return false;
        }

        static string Fail(int zeroBasedLine, string message) => "line " + (zeroBasedLine + 1) + ": " + message;

        static bool TryNumber(string s, out float value) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !float.IsNaN(value) && !float.IsInfinity(value);

        static bool TryOne(string[] values, out float value)
        {
            value = 0f;
            return values.Length == 1 && TryNumber(values[0], out value);
        }

        static bool TryPlanet(string[] values, out PlanetConfig planet)
        {
            planet = default;
            if (values.Length != 4 && values.Length != 5) return false;
            var n = new float[5];
            for (int i = 0; i < values.Length; i++)
                if (!TryNumber(values[i], out n[i])) return false;
            if (!(n[2] > 0f) || !(n[3] > 0f)) return false;
            planet = new PlanetConfig(new float2(n[0], n[1]), n[2], n[3], n[4]);
            return true;
        }
    }
}
