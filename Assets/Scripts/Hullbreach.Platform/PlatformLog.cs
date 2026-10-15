namespace Hullbreach.Platform
{
    // Engine-free code cannot call Debug.Log; the Unity layer adapts this to it.
    // Callers must never pass tokens, passwords or the server key.
    // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
    public interface IPlatformLog
    {
        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        void Info(string message);

        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        void Warning(string message);
    }

    // For tests and callers that do not care about diagnostics.
    // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
    public sealed class NullPlatformLog : IPlatformLog
    {
        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public static readonly NullPlatformLog Instance = new NullPlatformLog();

        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public void Info(string message)
        {
        }

        // frob:doc docs/platform-link.md#hullbreachplatform-module-reference
        public void Warning(string message)
        {
        }
    }
}
