using System;
using System.IO;

namespace Hullbreach.Platform
{
    // Remembers the platform session between launches (S07-3) in one small file under the
    // per-user data folder. Never logs the token; failures are logged and swallowed so a
    // read-only disk only costs the player a fresh sign-in.
    // frob:doc docs/platform-link.md#remembered-session
    public sealed class SessionTokenStore
    {
        readonly string _path;
        readonly IPlatformLog _log;

        // frob:doc docs/platform-link.md#remembered-session
        public SessionTokenStore(string path, IPlatformLog log = null)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is required", nameof(path));
            _path = path;
            _log = log ?? NullPlatformLog.Instance;
        }

        // Line 1 is the username, line 2 the token; usernames cannot contain newlines.
        // frob:doc docs/platform-link.md#remembered-session
        public bool Save(string username, string token)
        {
            if (string.IsNullOrEmpty(token) || (username ?? string.Empty).IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                _log.Warning("Hullbreach session store: refusing to save an unusable session.");
                return false;
            }
            try
            {
                string dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_path, (username ?? string.Empty) + "\n" + token + "\n");
                _log.Info($"Hullbreach session store: remembered the session for '{username}'.");
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _log.Warning($"Hullbreach session store: could not save the session ({e.GetType().Name}).");
                return false;
            }
        }

        // False when nothing usable is stored; a corrupt file is deleted.
        // frob:doc docs/platform-link.md#remembered-session
        public bool TryLoad(out string username, out string token)
        {
            username = string.Empty;
            token = string.Empty;
            try
            {
                if (!File.Exists(_path)) return false;
                string[] lines = File.ReadAllText(_path).Split('\n');
                string t = lines.Length >= 2 ? lines[1].Trim() : string.Empty;
                if (t.Length == 0)
                {
                    _log.Warning("Hullbreach session store: stored session is malformed; discarding it.");
                    Clear();
                    return false;
                }
                username = lines[0].Trim();
                token = t;
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _log.Warning($"Hullbreach session store: could not read the session ({e.GetType().Name}).");
                return false;
            }
        }

        // frob:doc docs/platform-link.md#remembered-session
        public void Clear()
        {
            try
            {
                if (!File.Exists(_path)) return;
                File.Delete(_path);
                _log.Info("Hullbreach session store: forgot the remembered session.");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                _log.Warning($"Hullbreach session store: could not delete the session ({e.GetType().Name}).");
            }
        }
    }
}
