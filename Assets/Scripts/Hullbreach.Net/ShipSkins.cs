namespace Hullbreach.Net
{
    // The platform's hull skins (slugs from its item catalogue) as a one-byte id the ghost stream can carry,
    // plus a minimal look for each: a tint and, for some, a stripe on alternate blocks. Id 0 is the default look;
    // an unknown or empty slug is the default too, so a new platform skin never breaks a match (S29-3c).
    // frob:ticket 01M5XACF0K75AVMM0MCNDDWXNE
    // frob:doc docs/reference/hullbreach-net.md#shipskins
    public static class ShipSkins
    {
        // frob:doc docs/reference/hullbreach-net.md#shipskins
        public readonly struct Look
        {
            public readonly float R, G, B;
            // 0 = no tint, 1 = fully the skin colour.
            public readonly float Mix;
            // Alternate blocks are darkened when true.
            public readonly bool Stripes;
            public Look(float r, float g, float b, float mix, bool stripes)
            {
                R = r; G = g; B = b; Mix = mix; Stripes = stripes;
            }
        }

        static readonly string[] Slugs =
        {
            "",
            "hull-skin-crimson-hull-wrap", "hull-skin-void-black-plating", "hull-skin-solar-flare-livery",
            "hull-skin-deep-sea-camo", "hull-skin-nebula-swirl-coat", "hull-skin-ashen-steel-finish",
            "hull-skin-polar-white-armor", "hull-skin-toxic-green-sheen", "hull-skin-royal-gold-trim",
            "hull-skin-carbon-fiber-weave", "hull-skin-ghost-chrome-plating", "hull-skin-magma-vein-hull",
            "hull-skin-aurora-prism-coat", "hull-skin-rustbelt-patina", "hull-skin-obsidian-glass-finish",
            "demo-hull-skin-rookie-stripes", "demo-hull-skin-rust-bucket",
        };

        static readonly Look[] Looks =
        {
            new Look(0, 0, 0, 0f, false),
            new Look(0.85f, 0.12f, 0.15f, 0.7f, false), new Look(0.05f, 0.05f, 0.08f, 0.8f, false),
            new Look(1f, 0.65f, 0.1f, 0.7f, true), new Look(0.12f, 0.35f, 0.4f, 0.7f, true),
            new Look(0.5f, 0.2f, 0.8f, 0.7f, true), new Look(0.6f, 0.6f, 0.62f, 0.6f, false),
            new Look(0.95f, 0.97f, 1f, 0.75f, false), new Look(0.3f, 0.95f, 0.2f, 0.65f, false),
            new Look(1f, 0.85f, 0.25f, 0.75f, true), new Look(0.1f, 0.1f, 0.12f, 0.75f, true),
            new Look(0.75f, 0.85f, 0.95f, 0.7f, false), new Look(1f, 0.3f, 0.05f, 0.7f, true),
            new Look(0.3f, 0.9f, 0.8f, 0.7f, true), new Look(0.55f, 0.35f, 0.2f, 0.7f, false),
            new Look(0.1f, 0.12f, 0.2f, 0.8f, false),
            new Look(0.95f, 0.35f, 0.3f, 0.65f, true), new Look(0.5f, 0.3f, 0.15f, 0.7f, false),
        };

        // 0 (default) for null, empty or an unknown slug.
        // frob:doc docs/reference/hullbreach-net.md#shipskins
        public static byte IdOf(string slug)
        {
            if (string.IsNullOrEmpty(slug)) return 0;
            string trimmed = slug.Trim();
            for (int i = 1; i < Slugs.Length; i++)
                if (Slugs[i] == trimmed) return (byte)i;
            return 0;
        }

        // Empty for the default or an unknown id.
        // frob:doc docs/reference/hullbreach-net.md#shipskins
        public static string SlugOf(byte id) => id < Slugs.Length ? Slugs[id] : string.Empty;

        // The default look (no tint) for an unknown id.
        // frob:doc docs/reference/hullbreach-net.md#shipskins
        public static Look LookOf(byte id) => id < Looks.Length ? Looks[id] : Looks[0];

        // How many skins are known (ids 1 through Count).
        // frob:doc docs/reference/hullbreach-net.md#shipskins
        public static int Count => Slugs.Length - 1;
    }
}
