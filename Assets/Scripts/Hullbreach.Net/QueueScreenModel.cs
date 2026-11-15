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

        // After this long in the queue with no opponent, the screen offers a bot match (P0-5).
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public const double BotOfferSeconds = 15.0;

        // True once a running search has waited BotOfferSeconds.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public static bool OffersBot(MatchmakingMachine machine) =>
            (machine.State == MatchmakingState.Queued && machine.ElapsedSeconds >= BotOfferSeconds)
            || machine.State == MatchmakingState.Failed;

        // Which bot to put first: 0 easy, 1 normal, 2 hard. A known rating decides (below 900 easy, below 1200 normal,
        // else hard); with none yet, a player who has not finished the Cadet Trial starts on easy.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public static int SuggestedBotTier(double rating, bool cadetTrialDone)
        {
            if (rating <= 0.0) return cadetTrialDone ? 1 : 0;
            return rating < 900.0 ? 0 : rating < 1200.0 ? 1 : 2;
        }

        // Failures are alerts (orange); everything else is plain ink.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public static bool IsAlert(MatchmakingMachine machine) => machine.State == MatchmakingState.Failed;
    }
}
