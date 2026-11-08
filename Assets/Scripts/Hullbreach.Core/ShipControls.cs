namespace Hullbreach.Core
{
    // The flight key map worded once, so the offline and online HUDs cannot disagree.
    // frob:ticket 01M6WV815GYEJ58MTM4HSS4Z6K
    // frob:doc docs/controls.md
    public static class ShipControls
    {
        // Thrust and steer keys (the Vertical/Horizontal axes accept both WASD and arrows).
        // frob:doc docs/controls.md
        public const string ThrustSteerLine = "W/S or Up/Down: thrust/reverse   A/D or Left/Right: steer";

        // Online fire and mode switch: fire is Space or left click, Tab toggles build.
        // frob:doc docs/controls.md
        public const string OnlineFireBuildLine = "Space/click: fire   Tab: build";
    }
}
