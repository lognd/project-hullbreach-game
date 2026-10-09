using System;
using System.Collections.Generic;

namespace Hullbreach.Settings
{
    // Player preferences the settings screen edits (S27); plain data so the
    // screen, the audio mixer and the display code all share one model.
    // frob:doc docs/reference/hullbreach-settings.md#gamesettings
    public sealed class GameSettings
    {
        // frob:doc docs/reference/hullbreach-settings.md#gamesettings
        public const float DefaultMasterVolume = 1f;

        // frob:doc docs/reference/hullbreach-settings.md#gamesettings
        public const float DefaultMusicVolume = 0.8f;

        // frob:doc docs/reference/hullbreach-settings.md#gamesettings
        public const float DefaultEffectsVolume = 1f;

        // frob:doc docs/reference/hullbreach-settings.md#gamesettings
        public const int DefaultWidth = 1920;

        // frob:doc docs/reference/hullbreach-settings.md#gamesettings
        public const int DefaultHeight = 1080;

        // Smallest and largest window edge accepted from a file; anything else is a hand-edit or corruption.
        // frob:doc docs/reference/hullbreach-settings.md#gamesettings
        public const int MinResolution = 320;

        // frob:doc docs/reference/hullbreach-settings.md#gamesettings
        public const int MaxResolution = 16384;

        // All volumes are 0..1; Normalize enforces it.
        // frob:doc docs/reference/hullbreach-settings.md#gamesettings
        public float MasterVolume = DefaultMasterVolume;

        // frob:doc docs/reference/hullbreach-settings.md#gamesettings
        public float MusicVolume = DefaultMusicVolume;

        // frob:doc docs/reference/hullbreach-settings.md#gamesettings
        public float EffectsVolume = DefaultEffectsVolume;

        // frob:doc docs/reference/hullbreach-settings.md#gamesettings
        public int ResolutionWidth = DefaultWidth;

        // frob:doc docs/reference/hullbreach-settings.md#gamesettings
        public int ResolutionHeight = DefaultHeight;

        // frob:doc docs/reference/hullbreach-settings.md#gamesettings
        public bool Fullscreen = true;

        // Placeholder for T-0049: action name to key name, persisted but not yet consumed.
        // Sorted so the file is byte-stable.
        // frob:doc docs/reference/hullbreach-settings.md#gamesettings
        public readonly SortedDictionary<string, string> KeyBindings = new SortedDictionary<string, string>(StringComparer.Ordinal);

        // frob:doc docs/reference/hullbreach-settings.md#gamesettings
        public static GameSettings Defaults() => new GameSettings();

        // Independent copy, so the screen can edit and cancel without touching the live settings.
        // frob:doc docs/reference/hullbreach-settings.md#gamesettings
        public GameSettings Clone()
        {
            var copy = new GameSettings
            {
                MasterVolume = MasterVolume,
                MusicVolume = MusicVolume,
                EffectsVolume = EffectsVolume,
                ResolutionWidth = ResolutionWidth,
                ResolutionHeight = ResolutionHeight,
                Fullscreen = Fullscreen,
            };
            foreach (var kvp in KeyBindings) copy.KeyBindings[kvp.Key] = kvp.Value;
            return copy;
        }

        // Clamps volumes to 0..1 and replaces an out-of-range resolution with the default.
        // frob:doc docs/reference/hullbreach-settings.md#gamesettings
        public void Normalize()
        {
            MasterVolume = Clamp01(MasterVolume);
            MusicVolume = Clamp01(MusicVolume);
            EffectsVolume = Clamp01(EffectsVolume);
            if (!ResolutionInRange(ResolutionWidth) || !ResolutionInRange(ResolutionHeight))
            {
                ResolutionWidth = DefaultWidth;
                ResolutionHeight = DefaultHeight;
            }
        }

        static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Min(1f, Math.Max(0f, v));

        static bool ResolutionInRange(int edge) => edge >= MinResolution && edge <= MaxResolution;
    }
}
