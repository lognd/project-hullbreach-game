using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hullbreach.Hud
{
    // Engine-free player settings (S27-1, S27-3, S37-2): volumes, resolution and the colorblind toggle.
    // frob:ticket 01M3DG5Y1JR30SDC29Y142APTZ
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public sealed class GameSettings
    {
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public const int MinResolution = 320;

        // Volumes are 0..1; setters clamp so a hand-edited file cannot blow out the mixer.
        float _master = 1f, _music = 0.8f, _effects = 1f;

        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public float MasterVolume { get => _master; set => _master = Clamp01(value); }

        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public float MusicVolume { get => _music; set => _music = Clamp01(value); }

        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public float EffectsVolume { get => _effects; set => _effects = Clamp01(value); }

        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public int ResolutionWidth { get; private set; } = 1280;

        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public int ResolutionHeight { get; private set; } = 720;

        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public bool Fullscreen { get; set; }

        // S37-2: draw stress with the colorblind-safe ramp.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public bool ColorblindPalette { get; set; }

        // Rejects sizes below MinResolution; returns whether the size was accepted.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public bool SetResolution(int width, int height)
        {
            if (width < MinResolution || height < MinResolution) return false;
            ResolutionWidth = width;
            ResolutionHeight = height;
            return true;
        }

        // One key=value per line, invariant culture, so the file survives a locale change.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public string Serialize()
        {
            var inv = CultureInfo.InvariantCulture;
            return "master=" + MasterVolume.ToString("R", inv) + "\n"
                + "music=" + MusicVolume.ToString("R", inv) + "\n"
                + "effects=" + EffectsVolume.ToString("R", inv) + "\n"
                + "width=" + ResolutionWidth.ToString(inv) + "\n"
                + "height=" + ResolutionHeight.ToString(inv) + "\n"
                + "fullscreen=" + (Fullscreen ? "1" : "0") + "\n"
                + "colorblind=" + (ColorblindPalette ? "1" : "0") + "\n";
        }

        // Unknown keys and unparsable values are skipped so older and newer builds share a file.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public static GameSettings Parse(string text)
        {
            var s = new GameSettings();
            var inv = CultureInfo.InvariantCulture;
            var seen = new Dictionary<string, string>();
            foreach (string raw in (text ?? string.Empty).Split('\n'))
            {
                int eq = raw.IndexOf('=');
                if (eq <= 0) continue;
                seen[raw.Substring(0, eq).Trim()] = raw.Substring(eq + 1).Trim();
            }
            if (seen.TryGetValue("master", out string v) && float.TryParse(v, NumberStyles.Float, inv, out float f)) s.MasterVolume = f;
            if (seen.TryGetValue("music", out v) && float.TryParse(v, NumberStyles.Float, inv, out f)) s.MusicVolume = f;
            if (seen.TryGetValue("effects", out v) && float.TryParse(v, NumberStyles.Float, inv, out f)) s.EffectsVolume = f;
            if (seen.TryGetValue("width", out string w) && seen.TryGetValue("height", out string h)
                && int.TryParse(w, NumberStyles.Integer, inv, out int wi) && int.TryParse(h, NumberStyles.Integer, inv, out int hi))
                s.SetResolution(wi, hi);
            if (seen.TryGetValue("fullscreen", out v)) s.Fullscreen = v == "1";
            if (seen.TryGetValue("colorblind", out v)) s.ColorblindPalette = v == "1";
            return s;
        }

        static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Min(1f, Math.Max(0f, v));
    }
}
