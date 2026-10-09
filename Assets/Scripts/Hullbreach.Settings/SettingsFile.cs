using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hullbreach.Settings
{
    // How a load ended: a file was read, there was none yet, or it could not be trusted.
    // frob:doc docs/reference/hullbreach-settings.md#settingsloadstatus
    public enum SettingsLoadStatus
    {
        Loaded,
        Missing,
        Corrupt,
    }

    // Settings plus how they were obtained; Settings is never null, defaults stand in on Missing/Corrupt.
    // frob:doc docs/reference/hullbreach-settings.md#settingsloadresult
    public readonly struct SettingsLoadResult
    {
        // frob:doc docs/reference/hullbreach-settings.md#settingsloadresult
        public readonly GameSettings Settings;

        // frob:doc docs/reference/hullbreach-settings.md#settingsloadresult
        public readonly SettingsLoadStatus Status;

        // One line per value that was ignored or clamped; empty for a clean file.
        // frob:doc docs/reference/hullbreach-settings.md#settingsloadresult
        public readonly IReadOnlyList<string> Warnings;

        // frob:doc docs/reference/hullbreach-settings.md#settingsloadresult
        public SettingsLoadResult(GameSettings settings, SettingsLoadStatus status, IReadOnlyList<string> warnings)
        {
            Settings = settings;
            Status = status;
            Warnings = warnings;
        }
    }

    // The versioned text format ("key value" lines); see docs/reference/hullbreach-settings.md#settingsfile.
    // frob:doc docs/reference/hullbreach-settings.md#settingsfile
    public static class SettingsFile
    {
        // frob:doc docs/reference/hullbreach-settings.md#settingsfile
        public const int CurrentVersion = 1;

        const string Magic = "hullbreach-settings";

        // frob:doc docs/reference/hullbreach-settings.md#settingsfile
        public static string Write(GameSettings s)
        {
            var sb = new StringBuilder();
            sb.Append(Magic).Append(' ').Append(CurrentVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
            Line(sb, "master_volume", Float(s.MasterVolume));
            Line(sb, "music_volume", Float(s.MusicVolume));
            Line(sb, "effects_volume", Float(s.EffectsVolume));
            Line(sb, "resolution_width", s.ResolutionWidth.ToString(CultureInfo.InvariantCulture));
            Line(sb, "resolution_height", s.ResolutionHeight.ToString(CultureInfo.InvariantCulture));
            Line(sb, "fullscreen", s.Fullscreen ? "true" : "false");
            foreach (var kvp in s.KeyBindings) Line(sb, "bind." + kvp.Key, kvp.Value);
            return sb.ToString();
        }

        // Never throws: a bad header gives Corrupt + defaults, a bad value keeps that field's default.
        // frob:doc docs/reference/hullbreach-settings.md#settingsfile
        public static SettingsLoadResult Parse(string text)
        {
            var warnings = new List<string>();
            var s = GameSettings.Defaults();
            if (string.IsNullOrWhiteSpace(text)) return Corrupt(warnings, "settings file is empty");

            string[] lines = text.Split('\n');
            string[] header = lines[0].Trim().Split(' ');
            if (header.Length != 2 || header[0] != Magic
                || !int.TryParse(header[1], NumberStyles.None, CultureInfo.InvariantCulture, out int version) || version < 1)
            {
                return Corrupt(warnings, "not a hullbreach settings file");
            }
            if (version > CurrentVersion) warnings.Add($"settings version {version} is newer than {CurrentVersion}; unknown keys ignored");

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                int space = line.IndexOf(' ');
                string key = space < 0 ? line : line.Substring(0, space);
                string value = space < 0 ? "" : line.Substring(space + 1).Trim();
                if (!Apply(s, key, value, out string problem)) warnings.Add($"line {i + 1}: {problem}");
            }

            s.Normalize();
            return new SettingsLoadResult(s, SettingsLoadStatus.Loaded, warnings);
        }

        static bool Apply(GameSettings s, string key, string value, out string problem)
        {
            problem = null;
            switch (key)
            {
                case "master_volume": return SetFloat(value, v => s.MasterVolume = v, key, out problem);
                case "music_volume": return SetFloat(value, v => s.MusicVolume = v, key, out problem);
                case "effects_volume": return SetFloat(value, v => s.EffectsVolume = v, key, out problem);
                case "resolution_width": return SetInt(value, v => s.ResolutionWidth = v, key, out problem);
                case "resolution_height": return SetInt(value, v => s.ResolutionHeight = v, key, out problem);
                case "fullscreen":
                    if (value == "true" || value == "false") { s.Fullscreen = value == "true"; return true; }
                    problem = "fullscreen must be true or false";
                    return false;
            }

            if (key.StartsWith("bind.", StringComparison.Ordinal) && key.Length > 5)
            {
                s.KeyBindings[key.Substring(5)] = value;
                return true;
            }

            problem = $"unknown key '{key}'";
            return false;
        }

        static bool SetFloat(string value, Action<float> assign, string key, out string problem)
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && !float.IsInfinity(f))
            {
                assign(f);
                problem = null;
                return true;
            }
            problem = $"{key} is not a number";
            return false;
        }

        static bool SetInt(string value, Action<int> assign, string key, out string problem)
        {
            if (int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int n))
            {
                assign(n);
                problem = null;
                return true;
            }
            problem = $"{key} is not an integer";
            return false;
        }

        static SettingsLoadResult Corrupt(List<string> warnings, string why)
        {
            warnings.Add(why);
            return new SettingsLoadResult(GameSettings.Defaults(), SettingsLoadStatus.Corrupt, warnings);
        }

        static string Float(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        static void Line(StringBuilder sb, string key, string value) => sb.Append(key).Append(' ').Append(value).Append('\n');
    }
}
