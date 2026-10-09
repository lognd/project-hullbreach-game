namespace Hullbreach.Settings
{
    // Outcome of a save; Error is null when Ok.
    // frob:doc docs/reference/hullbreach-settings.md#settingssaveresult
    public readonly struct SettingsSaveResult
    {
        // frob:doc docs/reference/hullbreach-settings.md#settingssaveresult
        public readonly bool Ok;

        // frob:doc docs/reference/hullbreach-settings.md#settingssaveresult
        public readonly string Error;

        // frob:doc docs/reference/hullbreach-settings.md#settingssaveresult
        public SettingsSaveResult(bool ok, string error)
        {
            Ok = ok;
            Error = error;
        }
    }

    // Loads settings at launch and saves them on change, over any ISettingsStorage.
    // frob:doc docs/reference/hullbreach-settings.md#settingsstore
    public sealed class SettingsStore
    {
        readonly ISettingsStorage _storage;

        // frob:doc docs/reference/hullbreach-settings.md#settingsstore
        public SettingsStore(ISettingsStorage storage)
        {
            _storage = storage ?? throw new System.ArgumentNullException(nameof(storage));
        }

        // Always returns usable settings: defaults when the file is missing, unreadable or corrupt.
        // The bad file is left on disk untouched until the next Save.
        // frob:doc docs/reference/hullbreach-settings.md#settingsstore
        public SettingsLoadResult Load()
        {
            switch (_storage.Read(out string text))
            {
                case ReadOutcome.Found:
                    return SettingsFile.Parse(text);
                case ReadOutcome.Missing:
                    return new SettingsLoadResult(GameSettings.Defaults(), SettingsLoadStatus.Missing, System.Array.Empty<string>());
                default:
                    return new SettingsLoadResult(GameSettings.Defaults(), SettingsLoadStatus.Corrupt, new[] { "settings file could not be read" });
            }
        }

        // Normalizes a copy before writing so a bad slider value never reaches the file.
        // frob:doc docs/reference/hullbreach-settings.md#settingsstore
        public SettingsSaveResult Save(GameSettings settings)
        {
            var copy = settings.Clone();
            copy.Normalize();
            bool ok = _storage.TryWrite(SettingsFile.Write(copy), out string error);
            return new SettingsSaveResult(ok, error);
        }
    }
}
