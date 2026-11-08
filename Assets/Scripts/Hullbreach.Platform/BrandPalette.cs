namespace Hullbreach.Platform
{
    // Brand colors shared with the platform web, as 0xRRGGBB so the engine-free tests can pin them.
    // frob:doc docs/platform-link.md#screen-style
    // frob:ticket 01M6WV5HP3KVSZNJT6H99MFS3Z
    public static class BrandPalette
    {
        // Page and camera background.
        public const uint Background = 0x0B1620;

        // Main panel fill.
        public const uint Panel = 0x0F1D2A;

        // Inset panels and secondary buttons, one step above Panel.
        public const uint Raised = 0x172B3C;

        // Body text.
        public const uint Ink = 0xD6E4EC;

        // Hints, placeholders and disabled text.
        public const uint Muted = 0x7F96A6;

        // Section titles, links and focus.
        public const uint Cyan = 0x5CC8E8;

        // Primary button and alerts only.
        public const uint Orange = 0xFF6A1F;

        // The line under the title on the sign-in screen.
        public const string Tagline = "Build. Breach. Claim the void.";

        // Red channel of a packed color, 0..255.
        public static int R(uint rgb) => (int)((rgb >> 16) & 0xFF);

        // Green channel of a packed color, 0..255.
        public static int G(uint rgb) => (int)((rgb >> 8) & 0xFF);

        // Blue channel of a packed color, 0..255.
        public static int B(uint rgb) => (int)(rgb & 0xFF);
    }
}
