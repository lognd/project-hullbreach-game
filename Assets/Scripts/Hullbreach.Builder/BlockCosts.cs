using Hullbreach.Core;

namespace Hullbreach.Builder
{
    // Point price of every block type and the pre-match build budget, so both ships start equal (S45).
    // Cost is build economy, not physics, so it lives in Builder; BlockPalette reads it from here.
    // frob:ticket 01M7EYG4KH9BT32Q4ERZVBQ3M4
    // frob:doc docs/reference/hullbreach-builder.md#blockcosts
    public readonly struct BlockCosts
    {
        // frob:doc docs/reference/hullbreach-builder.md#blockcosts
        public const int DefaultBudget = 60;
        // frob:doc docs/reference/hullbreach-builder.md#blockcosts
        public const int DefaultMaxBlocks = 40;
        // frob:doc docs/reference/hullbreach-builder.md#blockcosts
        public const int DefaultWeldedSurcharge = 2;

        readonly int[] _cost;

        // Points a ship may spend on blocks before the match; the core is free.
        // frob:doc docs/reference/hullbreach-builder.md#blockcosts
        public readonly int Budget;
        // Hard cap on blocks per ship (core included), before and during the match.
        // frob:doc docs/reference/hullbreach-builder.md#blockcosts
        public readonly int MaxBlocks;
        // Extra points for a welded block (the builder may not remove it).
        // frob:doc docs/reference/hullbreach-builder.md#blockcosts
        public readonly int WeldedSurcharge;

        // costs needs one entry per BlockTypes entry; null takes the defaults.
        // frob:doc docs/reference/hullbreach-builder.md#blockcosts
        public BlockCosts(int[] costs, int budget, int maxBlocks, int weldedSurcharge)
        {
            _cost = costs != null ? (int[])costs.Clone() : DefaultTable();
            Budget = budget;
            MaxBlocks = maxBlocks;
            WeldedSurcharge = weldedSurcharge;
        }

        static int[] DefaultTable() => new[]
        {
            0, // Core
            1, // Hull
            3, // Armor
            4, // Thruster
            5, // Cannon
            2, // Fin
            3, // RetroThruster
        };

        // frob:doc docs/reference/hullbreach-builder.md#blockcosts
        public static BlockCosts Default() => new BlockCosts(null, DefaultBudget, DefaultMaxBlocks, DefaultWeldedSurcharge);

        // Null when valid, else the problem; the loader and the server both call it.
        // frob:doc docs/reference/hullbreach-builder.md#blockcosts
        public string Validate()
        {
            if (_cost == null || _cost.Length != BlockTypes.Count) return "costs need one entry per block type";
            foreach (int c in _cost) if (c < 0) return "costs must be >= 0";
            if (_cost[BlockTypes.Core] != 0) return "the core must cost 0";
            if (Budget < 0) return "budget must be >= 0";
            if (MaxBlocks < 1 || MaxBlocks > 4096) return "maxBlocks must be within 1..4096";
            if (WeldedSurcharge < 0) return "weldedSurcharge must be >= 0";
            return null;
        }

        // Unknown types cost int.MaxValue so a bad id can never look affordable.
        // frob:doc docs/reference/hullbreach-builder.md#blockcosts
        public int CostOf(byte typeId, bool welded = false)
        {
            if (_cost == null || typeId >= _cost.Length) return int.MaxValue;
            return _cost[typeId] + (welded && typeId != BlockTypes.Core ? WeldedSurcharge : 0);
        }

        // Same table with one price changed (the JSON loader's building block).
        // frob:doc docs/reference/hullbreach-builder.md#blockcosts
        public BlockCosts WithCost(byte typeId, int cost)
        {
            var copy = (int[])(_cost ?? DefaultTable()).Clone();
            if (typeId < copy.Length) copy[typeId] = cost;
            return new BlockCosts(copy, Budget, MaxBlocks, WeldedSurcharge);
        }

        // frob:doc docs/reference/hullbreach-builder.md#blockcosts
        public BlockCosts WithLimits(int budget, int maxBlocks, int weldedSurcharge) =>
            new BlockCosts(_cost, budget, maxBlocks, weldedSurcharge);

        // Total points a grid has spent (welded blocks pay the surcharge).
        // frob:doc docs/reference/hullbreach-builder.md#blockcosts
        public int CostOf(BlockGrid grid)
        {
            long total = 0;
            foreach (var kvp in grid.All) total += CostOf(kvp.Value.TypeId, kvp.Value.IsWelded);
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }
    }
}
