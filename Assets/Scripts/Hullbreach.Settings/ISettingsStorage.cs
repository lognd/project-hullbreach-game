namespace Hullbreach.Settings
{
    // What a storage read found; Unreadable (locked, no permission) is not the same as first launch.
    // frob:doc docs/reference/hullbreach-settings.md#readoutcome
    public enum ReadOutcome
    {
        Found,
        Missing,
        Unreadable,
    }

    // The one seam between settings and the disk, so tests and tools can swap it.
    // frob:doc docs/reference/hullbreach-settings.md#isettingsstorage
    public interface ISettingsStorage
    {
        // Missing and Unreadable leave text null.
        // frob:doc docs/reference/hullbreach-settings.md#isettingsstorage
        ReadOutcome Read(out string text);

        // False with a reason on failure; never throws for I/O problems.
        // frob:doc docs/reference/hullbreach-settings.md#isettingsstorage
        bool TryWrite(string text, out string error);
    }
}
