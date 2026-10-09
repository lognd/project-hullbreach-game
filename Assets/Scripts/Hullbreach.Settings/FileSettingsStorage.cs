using System;
using System.IO;

namespace Hullbreach.Settings
{
    // Stores the settings text in one local file, replacing it via a temp file
    // so a crash mid-save cannot leave a half-written settings file.
    // frob:doc docs/reference/hullbreach-settings.md#filesettingsstorage
    public sealed class FileSettingsStorage : ISettingsStorage
    {
        readonly string _path;

        // The caller picks the path (Game passes persistentDataPath) so this stays engine-free.
        // frob:doc docs/reference/hullbreach-settings.md#filesettingsstorage
        public FileSettingsStorage(string path)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
        }

        // frob:doc docs/reference/hullbreach-settings.md#filesettingsstorage
        public ReadOutcome Read(out string text)
        {
            text = null;
            if (!File.Exists(_path)) return ReadOutcome.Missing;
            try
            {
                text = File.ReadAllText(_path);
                return ReadOutcome.Found;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return ReadOutcome.Unreadable;
            }
        }

        // frob:doc docs/reference/hullbreach-settings.md#filesettingsstorage
        public bool TryWrite(string text, out string error)
        {
            string temp = _path + ".tmp";
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(_path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(temp, text);
                File.Copy(temp, _path, true);
                File.Delete(temp);
                error = null;
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }
        }
    }
}
