namespace Hullbreach.Hud
{
    // Where each entry of the Play hub leads. Only the online queue needs an account; the bot, the Cadet Trial
    // and LAN play work as a guest, so a player never meets a sign-in form just to start playing.
    public enum HubDestination { Bot, CadetTrial, OnlineQueue, SignIn, Lan, Title }

    public static class HubMenuModel
    {
        public static readonly string[] BotTierNames = { "EASY", "NORMAL", "HARD" };

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
