namespace Hullbreach.Hud
{
    // Where each entry of the Play hub leads. Only the online queue needs an account; the bot, the Cadet Trial
    // and LAN play work as a guest, so a player never meets a sign-in form just to start playing.
    public enum HubDestination { Bot, CadetTrial, OnlineQueue, SignIn, Lan, Title }

    public static class HubMenuModel
    {
        public static readonly string[] BotTierNames = { "EASY", "NORMAL", "HARD" };

        // One line under each difficulty so a newcomer knows what to pick.
        // frob:doc docs/design/ux-gx.md#newcomer-defaults
        public static readonly string[] BotTierDescriptions =
            { "Easy: forgiving practice", "Normal: a fair fight", "Hard: expert" };

        // The tier a bot match starts on: the last one used when there is one; otherwise EASY for a player with no
        // completed match, and the rating's suggestion (see QueueScreenModel.SuggestedBotTier) for everyone else.
        // `remembered` is -1 when nothing is stored; `fallbackTier` is used for a player who has finished a match.
        // frob:doc docs/design/ux-gx.md#newcomer-defaults
        public static int DefaultBotTier(int remembered, bool anyMatchCompleted, int fallbackTier)
        {
            if (remembered >= 0 && remembered < BotTierNames.Length) return remembered;
            if (!anyMatchCompleted) return 0;
            return fallbackTier >= 0 && fallbackTier < BotTierNames.Length ? fallbackTier : 1;
        }

        // Button text: the name, starred when it is the default pick.
        // frob:doc docs/design/ux-gx.md#newcomer-defaults
        public static string TierLabel(int tier, int defaultTier)
        {
            if (tier < 0 || tier >= BotTierNames.Length) return string.Empty;
            return BotTierNames[tier] + (tier == defaultTier ? " *" : string.Empty);
        }

        // Two-line button text for TMP: the label, then the part of the description after the colon in small type.
        // frob:doc docs/design/ux-gx.md#newcomer-defaults
        public static string TierLabelWithHint(int tier, int defaultTier)
        {
            string label = TierLabel(tier, defaultTier);
            string desc = TierDescription(tier);
            int colon = desc.IndexOf(':');
            string hint = colon >= 0 ? desc.Substring(colon + 1).Trim() : desc;
            return hint.Length == 0 ? label : label + "\n<size=60%>" + hint + "</size>";
        }

        // The description line for a tier, empty when out of range.
        // frob:doc docs/design/ux-gx.md#newcomer-defaults
        public static string TierDescription(int tier)
            => tier >= 0 && tier < BotTierDescriptions.Length ? BotTierDescriptions[tier] : string.Empty;

        // True when the entry needs the player to be signed in first.
        public static bool NeedsSignIn(HubDestination destination) => destination == HubDestination.OnlineQueue;

        // The step the Online button takes: straight to the queue when signed in, else to the sign-in form.
        public static HubDestination OnlineTarget(bool signedIn) => signedIn ? HubDestination.OnlineQueue : HubDestination.SignIn;

        public static string OnlineLabel(bool signedIn) => signedIn ? "ONLINE QUEUE  (ranked)" : "ONLINE QUEUE  (sign in)";

        public static string AccountLine(bool signedIn, bool offline, string username)
        {
            if (signedIn) return "Signed in as " + username;
            return offline ? "Playing as guest" : "Not signed in - guests can play bots and LAN";
        }
    }
}
