using System;

namespace Hullbreach.Hud
{
    // Logic behind the settings screen (S27-1, S37-2): edits GameSettings and saves them on Apply.
    // frob:ticket 01M3DG5Y1GSP9K4A0MY9MSHS37
    // frob:ticket 01M3DG5Y2DX8GJYANMCGM0B19P
    // frob:doc docs/design/ui-port.md#settings-screen
    public sealed class SettingsScreenModel
    {
        // Resolution choices the screen cycles through; a saved size outside the list is kept as-is.
        // frob:doc docs/design/ui-port.md#settings-screen
        public static readonly (int Width, int Height)[] Resolutions =
        {
            (1280, 720), (1600, 900), (1920, 1080), (2560, 1440),
        };

        readonly GameSettingsStore _store;
        readonly Action<string> _log;

        // The log callback receives one line per change worth recording; null discards them.
        // frob:doc docs/design/ui-port.md#settings-screen
        public SettingsScreenModel(GameSettings settings, GameSettingsStore store, Action<string> log = null)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _log = log ?? (_ => { });
        }

        // The settings being edited; the screen reads them back for display.
        // frob:doc docs/design/ui-port.md#settings-screen
        public GameSettings Settings { get; }

        // Status line shown under the controls (saved, or a failed write).
        // frob:doc docs/design/ui-port.md#settings-screen
        public string Message { get; private set; } = string.Empty;

        // True when the last Apply could not write the file.
        // frob:doc docs/design/ui-port.md#settings-screen
        public bool IsError { get; private set; }

        // Raised after any edit or apply so the view can redraw.
        // frob:doc docs/design/ui-port.md#settings-screen
        public event Action Changed;

        // Volume as the whole percent shown beside a slider, for example "80%".
        // frob:doc docs/design/ui-port.md#settings-screen
        public static string PercentLabel(float volume) =>
            ((int)Math.Round(Math.Max(0f, Math.Min(1f, volume)) * 100f)).ToString() + "%";

        // Resolution as "1280 x 720".
        // frob:doc docs/design/ui-port.md#settings-screen
        public string ResolutionLabel => Settings.ResolutionWidth + " x " + Settings.ResolutionHeight;

        // Index of the current size in Resolutions, or -1 when it is a custom saved size.
        // frob:doc docs/design/ui-port.md#settings-screen
        public int ResolutionIndex
        {
            get
            {
                for (int i = 0; i < Resolutions.Length; i++)
                {
                    if (Resolutions[i].Width == Settings.ResolutionWidth &&
                        Resolutions[i].Height == Settings.ResolutionHeight) return i;
                }
                return -1;
            }
        }

        // Master volume from a slider; clamped by GameSettings.
        // frob:doc docs/design/ui-port.md#settings-screen
        public void SetMaster(float value) { Settings.MasterVolume = value; Touch(); }

        // Music volume from a slider.
        // frob:doc docs/design/ui-port.md#settings-screen
        public void SetMusic(float value) { Settings.MusicVolume = value; Touch(); }

        // Effects volume from a slider.
        // frob:doc docs/design/ui-port.md#settings-screen
        public void SetEffects(float value) { Settings.EffectsVolume = value; Touch(); }

        // Fullscreen toggle.
        // frob:doc docs/design/ui-port.md#settings-screen
        public void SetFullscreen(bool on) { Settings.Fullscreen = on; Touch(); }

        // Colorblind-safe stress palette toggle (S37-2); ShipRenderer reads it from the saved file.
        // frob:doc docs/design/ui-port.md#settings-screen
        public void SetColorblind(bool on)
        {
            Settings.ColorblindPalette = on;
            _log("Hullbreach settings: colorblind palette " + (on ? "on" : "off") + ".");
            Touch();
        }

        // Steps to the next (+1) or previous (-1) preset, wrapping; a custom size starts at the ends.
        // frob:doc docs/design/ui-port.md#settings-screen
        public void StepResolution(int direction)
        {
            int count = Resolutions.Length;
            int index = ResolutionIndex;
            int next = index < 0 ? (direction >= 0 ? 0 : count - 1) : ((index + (direction >= 0 ? 1 : -1)) % count + count) % count;
            Settings.SetResolution(Resolutions[next].Width, Resolutions[next].Height);
            _log("Hullbreach settings: resolution " + ResolutionLabel + ".");
            Touch();
        }

        // Writes the settings; false (and an error message) when the disk refuses.
        // frob:doc docs/design/ui-port.md#settings-screen
        public bool Apply()
        {
            bool ok = _store.Save(Settings);
            IsError = !ok;
            Message = ok ? "Settings saved." : "Could not save settings.";
            Changed?.Invoke();
            return ok;
        }

        void Touch()
        {
            Message = string.Empty;
            IsError = false;
            Changed?.Invoke();
        }
    }
}
