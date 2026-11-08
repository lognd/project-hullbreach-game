using System;
using System.IO;

namespace Hullbreach.Hud
{
    // Persists GameSettings to one small file (S27-3). Failures are logged and swallowed:
    // a read-only disk costs the player their settings, never the game.
    // frob:ticket 01M3DG5Y1JR30SDC29Y142APTZ
    // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
    public sealed class GameSettingsStore
    {
        readonly string _path;
        readonly Action<string> _log;

        // The log callback receives one line per load, save or failure; null discards them.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public GameSettingsStore(string path, Action<string> log = null)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is required", nameof(path));
            _path = path;
            _log = log ?? (_ => { });
        }

        // Defaults when the file is missing or unreadable.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public GameSettings Load()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    _log("Hullbreach settings: no saved settings; using defaults.");
                    return new GameSettings();
                }
                _log("Hullbreach settings: loaded saved settings.");
                return GameSettings.Parse(File.ReadAllText(_path));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _log($"Hullbreach settings: could not read settings ({e.GetType().Name}); using defaults.");
                return new GameSettings();
            }
        }

        // False when the write failed.
        // frob:doc docs/design/ui-port.md#hullbreachhud-module-reference
        public bool Save(GameSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            try
            {
                string dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_path, settings.Serialize());
                _log("Hullbreach settings: saved.");
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _log($"Hullbreach settings: could not save settings ({e.GetType().Name}).");
                return false;
            }
        }
    }
}
