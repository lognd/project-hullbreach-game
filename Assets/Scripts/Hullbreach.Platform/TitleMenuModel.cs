using System.Collections.Generic;

namespace Hullbreach.Platform
{
    // What a title-screen button does when pressed (S26-1).
    // frob:doc docs/platform-link.md#title-screen
    public enum TitleAction
    {
        Play,
        Build,
        Settings,
        SignIn,
        Quit,
    }

    // One title-screen button: its action, label and whether it is the single primary (orange) one.
    // frob:doc docs/platform-link.md#title-screen
    public readonly struct TitleItem
    {
        // frob:doc docs/platform-link.md#title-screen
        public TitleItem(TitleAction action, string label, bool primary)
        {
            Action = action;
            Label = label;
            Primary = primary;
        }

        // frob:doc docs/platform-link.md#title-screen
        public TitleAction Action { get; }

        // frob:doc docs/platform-link.md#title-screen
        public string Label { get; }

        // frob:doc docs/platform-link.md#title-screen
        public bool Primary { get; }
    }

    // Engine-free content of the title screen: the five buttons and the signed-in line (S26-1..3).
    // frob:ticket 01M3DG5Y1D78FFHNKW4AB7YB9B
    // frob:ticket 01M3DG5Y1ECEZ8A5Y7AJT2NM7X
    // frob:ticket 01M3DG5Y1FGWV5H8MC0MW0A41P
    // frob:doc docs/platform-link.md#title-screen
    public static class TitleMenuModel
    {
        // Play, Build, Settings, Sign in, Quit in that order; only Play is primary.
        // frob:doc docs/platform-link.md#title-screen
        public static IReadOnlyList<TitleItem> Items(bool signedIn) => new[]
        {
            new TitleItem(TitleAction.Play, "PLAY", true),
            new TitleItem(TitleAction.Build, "BUILD", false),
            new TitleItem(TitleAction.Settings, "SETTINGS", false),
            new TitleItem(TitleAction.SignIn, signedIn ? "ACCOUNT" : "SIGN IN", false),
            new TitleItem(TitleAction.Quit, "QUIT", false),
        };

        // "Signed in as NAME", "Playing offline" or a prompt, for the corner of the screen.
        // frob:doc docs/platform-link.md#title-screen
        public static string AccountLine(bool signedIn, bool offline, string username)
        {
            if (signedIn) return "Signed in as " + (string.IsNullOrEmpty(username) ? "player" : username);
            return offline ? "Playing offline" : "Not signed in";
        }
    }
}
