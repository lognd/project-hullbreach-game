using System;
using System.Collections.Generic;
using Hullbreach.Core;

namespace Hullbreach.Builder
{
    // Preview shown to the UI for whatever cell the pointer is over.
    // frob:doc docs/reference/hullbreach-builder.md#hoverstate
    public readonly struct HoverState
    {
        // True when acting on Key right now would succeed.
        // frob:doc docs/reference/hullbreach-builder.md#hoverstate
        public readonly bool Valid;

        // frob:doc docs/reference/hullbreach-builder.md#hoverstate
        public readonly int Key;

        // Why Valid is what it is; Ok when Valid is true.
        // frob:doc docs/reference/hullbreach-builder.md#hoverstate
        public readonly PlacementVerdict Verdict;

        // frob:doc docs/reference/hullbreach-builder.md#hoverstate
        public HoverState(bool valid, int key, PlacementVerdict verdict)
        {
            Valid = valid;
            Key = key;
            Verdict = verdict;
        }
    }

    // The two-click placement state machine (S30). See
    // docs/reference/hullbreach-builder.md#builderstate.
    // frob:doc docs/reference/hullbreach-builder.md#builderstate
    public enum BuilderState { Idle, Orienting }

    // Owns the grid plus the click-driven state machine; pure C#, no
    // UnityEngine dependency. See docs/reference/hullbreach-builder.md#buildersession.
    // frob:doc docs/reference/hullbreach-builder.md#buildersession
    public sealed class BuilderSession
    {
        readonly UndoStack _undo = new UndoStack();

        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public BlockGrid Grid { get; }

        // Owns a brand-new grid, for standalone tests/callers.
        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public BuilderSession() : this(new BlockGrid())
        {
        }

        // Builds over an EXTERNAL grid so a demo scene's builder can edit
        // the same BlockGrid a ShipBody simulates; caller keeps ownership.
        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public BuilderSession(BlockGrid grid)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        // Defaults to Core so the very first click can seed the grid.
        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public byte SelectedTypeId { get; private set; } = BlockTypes.Core;

        // Drives what Click and Hover do.
        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public BuilderState State { get; private set; } = BuilderState.Idle;

        // The cell chosen on the first click, while Orienting.
        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public int PendingKey { get; private set; }

        // Facing previewed by the last Hover while Orienting (low 2 bits).
        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public byte PendingModifiers { get; private set; }

        // Fires after any mutation so UI can refresh.
        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public event Action Changed;

        // S33 criterion 2.
        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public float TotalMass => Grid.Mass.Total;

        // S33 criterion 2.
        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public int BlockCount => Grid.Count;

        // Cancels any pending orientation.
        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public void Select(byte typeId)
        {
            SelectedTypeId = typeId;
            Cancel();
        }

        // Previews what would happen at `key` right now; see
        // docs/reference/hullbreach-builder.md#buildersession.
        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public HoverState Hover(int key)
        {
            if (State == BuilderState.Orienting)
            {
                PendingModifiers = FacingTowards(PendingKey, key);
                bool candidateValid = PlacementRules.CanPlace(Grid, PendingKey, SelectedTypeId, PendingModifiers, out PlacementVerdict candidateWhy);
                return new HoverState(candidateValid, key, candidateWhy);
            }

            bool valid = PlacementRules.CanPlace(Grid, key, SelectedTypeId, 0, out PlacementVerdict why);
            return new HoverState(valid, key, why);
        }

        // The main two-click gesture; see docs/reference/hullbreach-builder.md#buildersession.
        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public bool Click(int key)
        {
            if (State == BuilderState.Orienting)
            {
                return CommitPending();
            }

            if (BlockPalette.IsSymmetric(SelectedTypeId))
            {
                if (!PlacementRules.CanPlace(Grid, key, SelectedTypeId, 0, out _)) return false;
                return Place(key, 0);
            }

            // The facing is not chosen yet, so the cell only needs to admit
            // SOME facing; Hover picks the exact one before it commits.
            if (!CanPlaceAnyFacing(key, SelectedTypeId)) return false;

            PendingKey = key;
            PendingModifiers = 0;
            State = BuilderState.Orienting;
            return true;
        }

        // True if any of the four cardinal facings would place `typeId` at `key`.
        bool CanPlaceAnyFacing(int key, byte typeId)
        {
            for (byte modifiers = 0; modifiers < 4; modifiers++)
            {
                if (PlacementRules.CanPlace(Grid, key, typeId, modifiers, out _)) return true;
            }
            return false;
        }

        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public void Cancel()
        {
            State = BuilderState.Idle;
            PendingKey = 0;
            PendingModifiers = 0;
        }

        // Applies the detach rule, recording it as one undoable action.
        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public bool Remove(int key)
        {
            if (!PlacementRules.CanRemove(Grid, key)) return false;

            // Snapshot every block BEFORE mutating, so Detach's stranded
            // keys can be recorded with their real block, not a default.
            var before = new Dictionary<int, Block>();
            foreach (var kvp in Grid.All) before[kvp.Key] = kvp.Value;

            var removedKeys = new List<int>();
            if (!PlacementRules.Detach(Grid, key, removedKeys)) return false;

            var entries = new List<(int Key, Block Block)>(removedKeys.Count);
            foreach (var removedKey in removedKeys)
            {
                entries.Add((removedKey, before[removedKey]));
            }
            _undo.RecordRemove(entries);

            Changed?.Invoke();
            return true;
        }

        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public bool Undo()
        {
            bool did = _undo.TryUndo(Grid);
            if (did) Changed?.Invoke();
            return did;
        }

        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public bool Redo()
        {
            bool did = _undo.TryRedo(Grid);
            if (did) Changed?.Invoke();
            return did;
        }

        // For UI/diagnostics.
        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public int UndoDepth => _undo.Depth;

        bool CommitPending()
        {
            int key = PendingKey;
            byte modifiers = PendingModifiers;

            // Validate BEFORE leaving Orienting: an invalid facing must
            // refuse the commit and leave the pending placement in place.
            if (!PlacementRules.CanPlace(Grid, key, SelectedTypeId, modifiers, out _)) return false;

            Cancel();
            return Place(key, modifiers);
        }

        bool Place(int key, byte modifiers)
        {
            if (!PlacementRules.CanPlace(Grid, key, SelectedTypeId, modifiers, out _)) return false;

            var block = new Block(SelectedTypeId, modifiers);
            if (!Grid.TryAdd(key, block)) return false;

            _undo.RecordPlace(key, block);
            Changed?.Invoke();
            return true;
        }

        // Short human text for the HUD explaining an invalid hover.
        // frob:doc docs/reference/hullbreach-builder.md#buildersession
        public static string DescribeVerdict(PlacementVerdict verdict) => verdict switch
        {
            PlacementVerdict.Ok => "ok",
            PlacementVerdict.OutOfRange => "outside the buildable area",
            PlacementVerdict.Occupied => "already occupied",
            PlacementVerdict.NotAdjacent => "must touch an existing block",
            PlacementVerdict.NeedsEmptyGrid => "the first block must be a core",
            PlacementVerdict.CoreAlreadyPlaced => "a core is already placed",
            PlacementVerdict.BlocksExhaust => "blocks the thruster exhaust",
            PlacementVerdict.BlocksMuzzle => "blocks the cannon's muzzle",
            PlacementVerdict.BlocksFin => "blocks the fin's clearance",
            PlacementVerdict.FinNeedsHull => "a fin needs a hull behind it",
            PlacementVerdict.InsideReservedCell => "sits inside another block's reserved space",
            _ => "invalid",
        };

        // Snaps from->to onto the nearest cardinal facing (0=+y,1=+x,2=-y,3=-x).
        static byte FacingTowards(int from, int to)
        {
            BlockKey.Unpack(from, out int fx, out int fy);
            BlockKey.Unpack(to, out int tx, out int ty);
            int dx = tx - fx;
            int dy = ty - fy;

            if (dx == 0 && dy == 0) return 0; // no direction yet; default to +y

            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                return dx >= 0 ? (byte)1 : (byte)3;
            }
            return dy >= 0 ? (byte)0 : (byte)2;
        }
    }
}
