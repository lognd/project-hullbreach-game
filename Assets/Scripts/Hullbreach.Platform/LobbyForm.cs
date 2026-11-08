using System.Globalization;

namespace Hullbreach.Platform
{
    // Lobby form rules kept out of the uGUI view so they are unit-tested.
    // frob:doc docs/netcode.md#lobby-keyboard-flow
    // frob:ticket 01M6WV5HP3KVSZNJT6H99MFS3Z
    public static class LobbyForm
    {
        // Smallest player count the host field accepts.
        public const int MinPlayers = 2;

        // Largest player count the host field accepts.
        public const int MaxPlayers = 16;

        // Default when the field is empty or not a number.
        public const int DefaultPlayers = 8;

        // Parses the max-players field, falling back to DefaultPlayers and clamping to the allowed range.
        public static int ParsePlayers(string text)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return DefaultPlayers;
            }

            return value < MinPlayers ? MinPlayers : value > MaxPlayers ? MaxPlayers : value;
        }

        // Problem with an optional lobby password, or null when it is fine.
        public static string PasswordProblem(string password) =>
            string.IsNullOrEmpty(password) || (password.Length >= 8 && password.Length <= 64)
                ? null
                : "Lobby passwords must be 8 to 64 characters.";

        // Problem with the typed join code, or null when it can be sent.
        public static string CodeProblem(string code) =>
            string.IsNullOrWhiteSpace(code) ? "Enter a lobby code." : null;

        // Esc leaves a joined room when idle; otherwise it does nothing (never an accidental quit).
        public static bool EscapeLeavesRoom(bool inRoom, bool busy) => inRoom && !busy;
    }
}
