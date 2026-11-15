using Hullbreach.Bots;
using Hullbreach.Net;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>Starts a local match against a bot: the lobby's "Play vs Bot" panel, the Cadet Trial, and the queue's bot offer.</summary>
    public static class HullbreachBotMatch
    {
        /// <summary>Hosts the match on loopback with a bot opponent. Needs the lobby launch's idle worlds.</summary>
        public static bool Start(BotDifficulty difficulty, bool trial, out string error)
        {
            HullbreachSparring.Request(difficulty);
            HullbreachSparring.Trial = trial;
            bool ok = HullbreachLanLobby.TryHost(DirectLaunchPlan.DefaultPort, out error);
            if (!ok) { HullbreachSparring.Requested = false; HullbreachSparring.Trial = false; }
            return ok;
        }
    }
}
