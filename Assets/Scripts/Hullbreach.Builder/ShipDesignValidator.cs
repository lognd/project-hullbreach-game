using System.Collections.Generic;
using Hullbreach.Core;

namespace Hullbreach.Builder
{
    // What is wrong with one block (or with the design as a whole).
    // frob:doc docs/reference/hullbreach-builder.md#designproblemkind
    public enum DesignProblemKind
    {
        MissingCore,
        UnknownType,
        BadValue,
        DuplicateCell,
        DamagedBlock,
        Placement,
    }

    // One reported problem; the design itself is left exactly as loaded.
    // frob:doc docs/reference/hullbreach-builder.md#designproblem
    public readonly struct DesignProblem
    {
        // frob:doc docs/reference/hullbreach-builder.md#designproblem
        public readonly DesignProblemKind Kind;

        // Cell of the offending block; meaningless for MissingCore.
        // frob:doc docs/reference/hullbreach-builder.md#designproblem
        public readonly int X;

        // frob:doc docs/reference/hullbreach-builder.md#designproblem
        public readonly int Y;

        // The PlacementRules reason when Kind is Placement, else Ok.
        // frob:doc docs/reference/hullbreach-builder.md#designproblem
        public readonly PlacementVerdict Verdict;

        // frob:doc docs/reference/hullbreach-builder.md#designproblem
        public DesignProblem(DesignProblemKind kind, int x, int y, PlacementVerdict verdict)
        {
            Kind = kind;
            X = x;
            Y = y;
            Verdict = verdict;
        }

        // Human text for a load report, reusing the HUD's verdict wording.
        // frob:doc docs/reference/hullbreach-builder.md#designproblem
        public override string ToString()
        {
            switch (Kind)
            {
                case DesignProblemKind.MissingCore: return "design has no core";
                case DesignProblemKind.UnknownType: return $"({X},{Y}): unknown block type";
                case DesignProblemKind.BadValue: return $"({X},{Y}): modifiers and damage must be 0..255";
                case DesignProblemKind.DuplicateCell: return $"({X},{Y}): cell listed more than once";
                case DesignProblemKind.DamagedBlock: return $"({X},{Y}): saved designs must be undamaged";
                default: return $"({X},{Y}): {BuilderSession.DescribeVerdict(Verdict)}";
            }
        }
    }

    // Checks a design against PlacementRules by replaying it into a scratch grid.
    // frob:doc docs/reference/hullbreach-builder.md#shipdesignvalidator
    public static class ShipDesignValidator
    {
        // Empty list means the design is valid; never mutates the design.
        // frob:doc docs/reference/hullbreach-builder.md#shipdesignvalidator
        public static List<DesignProblem> Validate(ShipDesign design)
        {
            var problems = new List<DesignProblem>();
            Replay(design, new BlockGrid(), problems);
            return problems;
        }

        // Core first, then sweeps in key order until no block is accepted; whatever
        // is left is reported with the verdict the rules give it against the accepted set.
        internal static void Replay(ShipDesign design, BlockGrid grid, List<DesignProblem> problems)
        {
            var pending = new SortedDictionary<int, Block>();
            bool anyCore = false;

            foreach (var b in design.Blocks)
            {
                if (b.TypeId < 0 || b.TypeId >= BlockTypes.Count)
                {
                    problems.Add(new DesignProblem(DesignProblemKind.UnknownType, b.X, b.Y, PlacementVerdict.Ok));
                    continue;
                }
                if (!BlockKey.InRange(b.X, b.Y))
                {
                    problems.Add(new DesignProblem(DesignProblemKind.Placement, b.X, b.Y, PlacementVerdict.OutOfRange));
                    continue;
                }
                if (b.Modifiers < 0 || b.Modifiers > 255 || b.Damage < 0 || b.Damage > 255)
                {
                    problems.Add(new DesignProblem(DesignProblemKind.BadValue, b.X, b.Y, PlacementVerdict.Ok));
                    continue;
                }

                int key = BlockKey.Pack(b.X, b.Y);
                if (pending.ContainsKey(key))
                {
                    problems.Add(new DesignProblem(DesignProblemKind.DuplicateCell, b.X, b.Y, PlacementVerdict.Ok));
                    continue;
                }
                if (b.Damage != 0)
                {
                    problems.Add(new DesignProblem(DesignProblemKind.DamagedBlock, b.X, b.Y, PlacementVerdict.Ok));
                }
                if (b.TypeId == BlockTypes.Core) anyCore = true;
                pending.Add(key, new Block((byte)b.TypeId, (byte)b.Modifiers, (byte)b.Damage));
            }

            if (!anyCore)
            {
                problems.Add(new DesignProblem(DesignProblemKind.MissingCore, 0, 0, PlacementVerdict.Ok));
                return;
            }

            foreach (var kvp in pending)
            {
                if (kvp.Value.TypeId != BlockTypes.Core) continue;
                grid.TryAdd(kvp.Key, kvp.Value);
                pending.Remove(kvp.Key);
                break;
            }

            bool progress = true;
            while (progress && pending.Count > 0)
            {
                progress = false;
                var accepted = new List<int>();
                foreach (var kvp in pending)
                {
                    if (!PlacementRules.CanPlace(grid, kvp.Key, kvp.Value.TypeId, kvp.Value.Modifiers, out _)) continue;
                    grid.TryAdd(kvp.Key, kvp.Value);
                    accepted.Add(kvp.Key);
                    progress = true;
                }
                foreach (int key in accepted) pending.Remove(key);
            }

            foreach (var kvp in pending)
            {
                PlacementRules.CanPlace(grid, kvp.Key, kvp.Value.TypeId, kvp.Value.Modifiers, out PlacementVerdict why);
                BlockKey.Unpack(kvp.Key, out int x, out int y);
                problems.Add(new DesignProblem(DesignProblemKind.Placement, x, y, why));
            }
        }
    }
}
