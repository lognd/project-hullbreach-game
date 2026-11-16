using Hullbreach.Bots;
using Hullbreach.Net;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>
    /// What the bot picker remembers (PlayerPrefs): the last difficulty a player started and how many matches they
    /// have finished, so a newcomer defaults to EASY and everyone else to what they used last.
    /// </summary>
    public static class HullbreachBotPrefs
    {
        const string TierKey = "hullbreach.bot_tier";
        const string DoneKey = "hullbreach.matches_completed";

        public static int LastTier
        {
            get { try { return UnityEngine.PlayerPrefs.GetInt(TierKey, -1); } catch { return -1; } }
        }

        public static bool AnyMatchCompleted
        {
            get { try { return UnityEngine.PlayerPrefs.GetInt(DoneKey, 0) > 0; } catch { return false; } }
        }

        public static void RememberTier(BotDifficulty difficulty)
        {
            try { UnityEngine.PlayerPrefs.SetInt(TierKey, (int)difficulty); UnityEngine.PlayerPrefs.Save(); } catch { }
        }

        public static void NoteMatchCompleted()
        {
            try { UnityEngine.PlayerPrefs.SetInt(DoneKey, UnityEngine.PlayerPrefs.GetInt(DoneKey, 0) + 1); UnityEngine.PlayerPrefs.Save(); } catch { }
        }

        /// <summary>The tier to highlight: last used, else EASY for a newcomer, else the rating's suggestion.</summary>
        public static int DefaultTier(double rating, bool cadetTrialDone)
            => Hullbreach.Hud.HubMenuModel.DefaultBotTier(LastTier, AnyMatchCompleted,
                QueueScreenModel.SuggestedBotTier(rating, cadetTrialDone));
    }

    /// <summary>Starts a local match against a bot: the lobby's "Play vs Bot" panel, the Cadet Trial, and the queue's bot offer.</summary>
    public static class HullbreachBotMatch
    {
        /// <summary>Hosts the match on loopback with a bot opponent. Needs the lobby launch's idle worlds.</summary>
        public static bool Start(BotDifficulty difficulty, bool trial, out string error)
        {
            HullbreachSparring.Request(difficulty);
            HullbreachSparring.Trial = trial;
            bool ok = HullbreachLanLobby.TryHost(DirectLaunchPlan.DefaultPort, out error);
            if (ok && !trial) HullbreachBotPrefs.RememberTier(difficulty);
            if (!ok) { HullbreachSparring.Requested = false; HullbreachSparring.Trial = false; }
            return ok;
        }
    }
}
