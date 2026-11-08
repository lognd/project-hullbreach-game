using Hullbreach.Platform;
using UnityEngine;

namespace Hullbreach.NetCode.Entities
{
    // BrandPalette as UnityEngine.Color for the uGUI views and the scene builder.
    // frob:doc docs/platform-link.md#screen-style
    // frob:ticket 01M6WV5HP3KVSZNJT6H99MFS3Z
    public static class BrandColors
    {
        // Page and camera background.
        public static Color Background => From(BrandPalette.Background);

        // Main panel fill.
        public static Color Panel => From(BrandPalette.Panel);

        // Inset panels and secondary buttons.
        public static Color Raised => From(BrandPalette.Raised);

        // Body text.
        public static Color Ink => From(BrandPalette.Ink);

        // Hints, placeholders and disabled text.
        public static Color Muted => From(BrandPalette.Muted);

        // Section titles and focus.
        public static Color Cyan => From(BrandPalette.Cyan);

        // Primary button and alerts only.
        public static Color Orange => From(BrandPalette.Orange);

        // Converts a packed 0xRRGGBB value to an opaque Color.
        public static Color From(uint rgb) =>
            new Color(BrandPalette.R(rgb) / 255f, BrandPalette.G(rgb) / 255f, BrandPalette.B(rgb) / 255f, 1f);
    }
}
