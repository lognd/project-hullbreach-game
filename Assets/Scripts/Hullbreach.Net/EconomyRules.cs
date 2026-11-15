using System.Collections.Generic;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Structure;
using Unity.Mathematics;

namespace Hullbreach.Net
{
    // The stateless build rules shared by the server (which enforces them) and the client HUD (which previews them),
    // so a refusal text can never disagree with the verdict.
    // frob:ticket 01M7EYG4KH9BT32Q4ERZVBQ3M4
    // frob:doc docs/reference/hullbreach-net.md#economyrules
    public static class EconomyRules
    {
        // Order matters: cooldown first (a double press is not a mistake), then geometry, then the economy.
        // `matchRunning` selects stock (mid-match) over budget (pre-match); `spare` is stock of `typeId`;
        // `budgetLeft` is points unspent. Never touches state and never needs the solver.
        // frob:doc docs/reference/hullbreach-net.md#economyrules
        public static PlacementResult CheckPlacement(BlockGrid grid, in EconomyConfig config, bool matchRunning,
            int spare, int budgetLeft, int cooldownTicks, int x, int y, byte typeId, byte modifiers, out byte detail)
        {
            detail = 0;
            if (matchRunning && cooldownTicks > 0) return PlacementResult.Cooldown;
            if (!BlockTypes.IsValid(typeId) || !BlockKey.InRange(x, y)) return PlacementResult.BadRequest;
            if (matchRunning && typeId == BlockTypes.Core) return PlacementResult.CoreNotBuildable;

            int key = BlockKey.Pack(x, y);
            if (!PlacementRules.CanPlace(grid, key, typeId, modifiers, out PlacementVerdict why))
            {
                detail = (byte)why;
                return why == PlacementVerdict.NotAdjacent ? PlacementResult.Disconnected : PlacementResult.BadRequest;
            }
            if (!config.Enabled) return PlacementResult.Ok;
            if (grid.Count >= config.Costs.MaxBlocks) return PlacementResult.SizeCap;
            if (matchRunning) return spare >= 1 ? PlacementResult.Ok : PlacementResult.OutOfStock;
            return config.Costs.CostOf(typeId) <= budgetLeft ? PlacementResult.Ok : PlacementResult.OverBudget;
        }

        // Pre-match points a grid still has to spend.
        // frob:doc docs/reference/hullbreach-net.md#economyrules
        public static int BudgetLeft(BlockGrid grid, in EconomyConfig config) => config.Costs.Budget - config.Costs.CostOf(grid);

        // True when adding `block` at `key` makes a block fail or buckle that was not already failing. Both runs use a
        // fresh solver under the ship's most recent loads, so the comparison is like for like; a ship already
        // overloaded is not blamed on the new block.
        // frob:doc docs/reference/hullbreach-net.md#economyrules
        public static bool WouldFailImmediately(BlockGrid grid, int key, Block block,
            IReadOnlyList<(float2 point, float2 force)> forces, float dt, int ticks = 12)
        {
            var after = new BlockGrid();
            foreach (var kv in grid.All) after.TryAdd(kv.Key, kv.Value);
            if (!after.TryAdd(key, block)) return false;
            HashSet<int> before = FailingBlocks(grid, forces, dt, ticks);
            HashSet<int> now = FailingBlocks(after, forces, dt, ticks);
            foreach (int k in now) if (!before.Contains(k)) return true;
            return false;
        }

        static HashSet<int> FailingBlocks(BlockGrid grid, IReadOnlyList<(float2 point, float2 force)> forces, float dt, int ticks)
        {
            var solver = new StructuralSolver
            {
                LoadScale = StructuralSolver.DefaultLoadScale,
                MaterialStiffnessScale = StructuralSolver.DefaultMaterialStiffnessScale,
                BucklingEveryNTicks = 1,
            };
            for (int i = 0; i < ticks; i++) solver.Tick(grid, forces, dt);
            var failing = new HashSet<int>();
            foreach (var kv in solver.BlockStresses)
                if (kv.Value.DuctileRatio >= 1f || kv.Value.BrittleRatio >= 1f) failing.Add(kv.Key);
            foreach (int k in solver.BuckledBlocks) failing.Add(k);
            return failing;
        }

        // Plain-language reason for a refusal, for a HUD toast. Empty for Ok.
        // frob:doc docs/reference/hullbreach-net.md#economyrules
        public static string Describe(PlacementResult result, byte detail = 0)
        {
            switch (result)
            {
                case PlacementResult.Ok: return string.Empty;
                case PlacementResult.NoShip: return "No ship to build on.";
                case PlacementResult.NotBuilding: return "Enter build mode first.";
                case PlacementResult.BadRequest: return detail != 0 ? "Cannot build there (" + ((PlacementVerdict)detail) + ")." : "Cannot build that.";
                case PlacementResult.Cooldown: return "Still building the last block.";
                case PlacementResult.OutOfStock: return "None of that block in stock. Salvage or loot some.";
                case PlacementResult.SizeCap: return "The ship is at its size limit.";
                case PlacementResult.OverBudget: return "Not enough build points left.";
                case PlacementResult.CoreNotBuildable: return "The core cannot be rebuilt.";
                case PlacementResult.Disconnected: return "A block must touch the ship.";
                case PlacementResult.WouldBuckle: return "The structure would fail under that block.";
                case PlacementResult.CannotRemove: return "That block cannot be removed.";
                default: return "Refused.";
            }
        }
    }
}
