using System.Globalization;

namespace Hullbreach.Net
{
    // Text and button state of the queue screen, derived from the matchmaking machine (S29-2).
    // frob:ticket 01M3DG5Y1PX311E24E62K5FAKG
    // frob:doc docs/reference/hullbreach-net.md#matchmaking
    public static class QueueScreenModel
    {
        // The headline: searching with elapsed time, match found, cancelled or the failure reason.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public static string Status(MatchmakingMachine machine)
        {
            switch (machine.State)
            {
                case MatchmakingState.Queued: return "Searching for an opponent... " + Elapsed(machine.ElapsedSeconds);
                case MatchmakingState.Matched: return "Match found. Connecting to " + machine.Handoff.Host + ".";
                case MatchmakingState.Cancelled: return "Search cancelled.";
                case MatchmakingState.Failed: return machine.FailureMessage;
                default: return "Ready to find a match.";
            }
        }

        // Seconds as m:ss, for example 75 -> "1:15".
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public static string Elapsed(double seconds)
        {
            int total = seconds < 0.0 ? 0 : (int)seconds;
            return (total / 60).ToString(CultureInfo.InvariantCulture) + ":" +
                (total % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        // Cancel is offered only while a search is running.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public static bool CanCancel(MatchmakingMachine machine) => machine.State == MatchmakingState.Queued;

        // Find match is offered when no search is running (idle, cancelled or failed).
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public static bool CanSearch(MatchmakingMachine machine) =>
            machine.State == MatchmakingState.Idle || machine.State == MatchmakingState.Cancelled ||
            machine.State == MatchmakingState.Failed;

        // Failures are alerts (orange); everything else is plain ink.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public static bool IsAlert(MatchmakingMachine machine) => machine.State == MatchmakingState.Failed;
    }
}
