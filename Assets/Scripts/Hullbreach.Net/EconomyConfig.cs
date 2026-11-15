using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.World;

namespace Hullbreach.Net
{
    // Every block-economy tunable in one place (S45): prices and budget, the starter kit, how fragile a
    // fresh block is, how much salvage and refund pay back, and the comeback trickle. Defaults are the
    // numbers docs/design/block-economy.md argues for; an optional "economy" block in the arena JSON overrides them.
    // frob:ticket 01M7EYG4KH9BT32Q4ERZVBQ3M4
    // frob:doc docs/reference/hullbreach-net.md#economyconfig
    public readonly struct EconomyConfig
    {
        // False runs the old rules: free building, no stock, no pickups.
        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public readonly bool Enabled;
        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public readonly BlockCosts Costs;
        // Blocks each peer starts the match with, one count per block type (the core entry is ignored).
        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public readonly int[] StartingStock;
        // Per-type stock cap.
        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public readonly int MaxStockPerType;
        // A freshly placed block starts with this Damage byte and heals to 0 over ConstructionSeconds.
        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public readonly float ConstructionSeconds;
        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public readonly byte ConstructionDamage;
        // Percent chance (deterministic per block) a destroyed block leaves salvage.
        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public readonly int SalvagePercent;
        // Speed at which salvage fans out from the wreck, on top of the debris velocity.
        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public readonly float SalvageSpread;
        // Percent of a block returned when it is deconstructed.
        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public readonly int RefundPercent;
        // Seconds between trickle blocks at the base rate; 0 or less turns the trickle off.
        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public readonly float TrickleSeconds;
        // Each block of deficit adds this fraction to the rate, up to TrickleMaxMultiplier.
        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public readonly float TrickleDeficitStep;
        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public readonly float TrickleMaxMultiplier;
        // The trickle pauses while this many blocks are already in stock.
        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public readonly int TrickleStockCap;
        // Lifetime cap per peer per match, so no pattern of damage can farm it.
        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public readonly int TrickleMatchCap;
        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public readonly PickupConfig Pickups;

        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public EconomyConfig(bool enabled, BlockCosts costs, int[] startingStock, int maxStockPerType,
            float constructionSeconds, byte constructionDamage, int salvagePercent, float salvageSpread,
            int refundPercent, float trickleSeconds, float trickleDeficitStep, float trickleMaxMultiplier,
            int trickleStockCap, int trickleMatchCap, PickupConfig pickups)
        {
            Enabled = enabled; Costs = costs; StartingStock = startingStock ?? new int[BlockTypes.Count];
            MaxStockPerType = maxStockPerType; ConstructionSeconds = constructionSeconds;
            ConstructionDamage = constructionDamage; SalvagePercent = salvagePercent; SalvageSpread = salvageSpread;
            RefundPercent = refundPercent; TrickleSeconds = trickleSeconds; TrickleDeficitStep = trickleDeficitStep;
            TrickleMaxMultiplier = trickleMaxMultiplier; TrickleStockCap = trickleStockCap;
            TrickleMatchCap = trickleMatchCap; Pickups = pickups;
        }

        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public static EconomyConfig Default()
        {
            var stock = new int[BlockTypes.Count];
            stock[BlockTypes.Hull] = 3;
            return new EconomyConfig(true, BlockCosts.Default(), stock, Inventory.DefaultMaxPerType,
                8f, 170, 70, 1.2f, 50, 20f, 0.25f, 3f, 6, 12, PickupConfig.Default());
        }

        // The same rules switched off (legacy free building); used by tests that predate the economy.
        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public EconomyConfig WithEnabled(bool enabled) => new EconomyConfig(enabled, Costs, StartingStock,
            MaxStockPerType, ConstructionSeconds, ConstructionDamage, SalvagePercent, SalvageSpread, RefundPercent,
            TrickleSeconds, TrickleDeficitStep, TrickleMaxMultiplier, TrickleStockCap, TrickleMatchCap, Pickups);

        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public EconomyConfig WithStartingStock(int[] stock) => new EconomyConfig(Enabled, Costs, stock,
            MaxStockPerType, ConstructionSeconds, ConstructionDamage, SalvagePercent, SalvageSpread, RefundPercent,
            TrickleSeconds, TrickleDeficitStep, TrickleMaxMultiplier, TrickleStockCap, TrickleMatchCap, Pickups);

        // Null when valid, else the first problem.
        // frob:doc docs/reference/hullbreach-net.md#economyconfig
        public string Validate()
        {
            string p = Costs.Validate() ?? Pickups.Validate();
            if (p != null) return p;
            if (StartingStock.Length != BlockTypes.Count) return "startingStock needs one entry per block type";
            foreach (int n in StartingStock) if (n < 0 || n > MaxStockPerType) return "startingStock entries must be within 0..maxStockPerType";
            if (MaxStockPerType < 1 || MaxStockPerType > 255) return "maxStockPerType must be within 1..255";
            if (ConstructionSeconds < 0f) return "constructionSeconds must be >= 0";
            if (SalvagePercent < 0 || SalvagePercent > 100) return "salvagePercent must be within 0..100";
            if (RefundPercent < 0 || RefundPercent > 100) return "refundPercent must be within 0..100";
            if (SalvageSpread < 0f) return "salvageSpread must be >= 0";
            if (TrickleDeficitStep < 0f || TrickleMaxMultiplier < 1f) return "trickle multiplier settings are out of range";
            if (TrickleStockCap < 0 || TrickleMatchCap < 0) return "trickle caps must be >= 0";
            return null;
        }
    }
}
