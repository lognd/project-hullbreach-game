using System;
using System.Collections.Generic;
using Hullbreach.Core;

namespace Hullbreach.Builder
{
    /// <summary>Preview shown to the UI for whatever cell the pointer is over.</summary>
    public readonly struct HoverState
    {
        /// <summary>True when acting on Key right now would succeed (place or, while orienting, commit).</summary>
        public readonly bool Valid;

        /// <summary>The hovered cell.</summary>
        public readonly int Key;

        /// <summary>Why Valid is what it is; PlacementVerdict.Ok when Valid is true.</summary>
        public readonly PlacementVerdict Verdict;

        public HoverState(bool valid, int key, PlacementVerdict verdict)
        {
            Valid = valid;
            Key = key;
            Verdict = verdict;
        }
    }

    /// <summary>
    /// The two-click placement state machine (S30). Idle is "nothing pending";
    /// Orienting is "a cell is chosen, waiting for a facing or a symmetric
    /// commit". Symmetric block types (Core/Hull/Armor) have no facing to
    /// choose, so S30's UI conversation ("skip the second click") applies and
    /// they commit on the first click.
    /// </summary>
    public enum BuilderState { Idle, Orienting }

    /// <summary>
    /// Owns the grid plus the click-driven state machine that turns palette
    /// selection and cell clicks into placements, orientations, removals and
    /// undo/redo. Pure C# -- no UnityEngine dependency -- so it is exercised
    /// directly in edit-mode tests; BuilderController in Hullbreach.Game is
    /// the thin MonoBehaviour that feeds it mouse input.
    /// </summary>
    public sealed class BuilderSession
    {
        readonly UndoStack _undo = new UndoStack();

        /// <summary>The ship under construction.</summary>
        public BlockGrid Grid { get; }

        /// <summary>Owns a brand-new grid -- the original behavior, used by
        /// standalone builder tests and any caller with no existing ship.</summary>
        public BuilderSession() : this(new BlockGrid())
        {
        }

        /// <summary>
        /// Build over an EXTERNAL grid instead of a private one, so the demo
        /// scene's builder can edit the very same BlockGrid a ShipBody is
        /// simulating -- otherwise placements would land in a grid nobody
        /// flies. Ownership stays with the caller; this session only mutates it.
        /// </summary>
        public BuilderSession(BlockGrid grid)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        /// <summary>Currently selected palette type, defaults to Core so the very first click can seed the grid.</summary>
        public byte SelectedTypeId { get; private set; } = BlockTypes.Core;

        /// <summary>Idle or Orienting; drives what Click and Hover do.</summary>
        public BuilderState State { get; private set; } = BuilderState.Idle;

        /// <summary>The cell chosen on the first click, while Orienting.</summary>
        public int PendingKey { get; private set; }

        /// <summary>The facing modifier previewed by the last Hover while Orienting (low 2 bits: 0=+y,1=+x,2=-y,3=-x).</summary>
        public byte PendingModifiers { get; private set; }

        /// <summary>Fires after any mutation (place, remove, undo, redo) so UI can refresh.</summary>
        public event Action Changed;

        /// <summary>Total mass of everything currently on the grid (S33 criterion 2).</summary>
        public float TotalMass => Grid.Mass.Total;

        /// <summary>Number of blocks currently on the grid (S33 criterion 2).</summary>
        public int BlockCount => Grid.Count;

        /// <summary>Choose which palette entry the next click will place. Cancels any pending orientation.</summary>
        public void Select(byte typeId)
        {
            SelectedTypeId = typeId;
            Cancel();
        }

        /// <summary>
        /// Preview what would happen at `key` right now: while Idle, whether it
        /// is a legal placement for the selected type; while Orienting, the
        /// cell hovered snaps the pending facing towards it, and the returned
        /// verdict re-validates the pending cell with that candidate facing --
        /// an orientation that would block its own exhaust/muzzle/fin
        /// clearance, or lacks a fin's hull anchor, previews as invalid.
        /// </summary>
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

        /// <summary>
        /// The main two-click gesture. Idle + valid cell: symmetric types
        /// commit immediately, asymmetric types enter Orienting. Orienting:
        /// commits the pending placement with the facing from the last Hover.
        /// </summary>
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
            // SOME facing (any of the four cardinals) -- the exact one is
            // picked by Hover and re-validated for real when the second
            // click commits it.
            if (!CanPlaceAnyFacing(key, SelectedTypeId)) return false;

            PendingKey = key;
            PendingModifiers = 0;
            State = BuilderState.Orienting;
            return true;
        }

        /// <summary>
        /// True when at least one of the four cardinal facings would make
        /// `typeId` placeable at `key` right now. Used only to decide whether
        /// a cell is even worth entering Orienting over, since the actual
        /// facing has not been chosen yet.
        /// </summary>
        bool CanPlaceAnyFacing(int key, byte typeId)
        {
            for (byte modifiers = 0; modifiers < 4; modifiers++)
            {
                if (PlacementRules.CanPlace(Grid, key, typeId, modifiers, out _)) return true;
            }
            return false;
        }

        /// <summary>Abandon the pending orientation without placing anything.</summary>
        public void Cancel()
        {
            State = BuilderState.Idle;
            PendingKey = 0;
            PendingModifiers = 0;
        }

        /// <summary>Remove the block at `key`, applying the detach rule, and record it as one undoable action.</summary>
        public bool Remove(int key)
        {
            if (!PlacementRules.CanRemove(Grid, key)) return false;

            // Snapshot every block currently on the grid BEFORE mutating it,
            // so whichever keys Detach ends up removing (the requested one
            // plus any stranded by the detach rule) can be recorded with
            // their real type/modifiers/damage rather than a fresh default.
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

        /// <summary>Undo the last placement or removal action.</summary>
        public bool Undo()
        {
            bool did = _undo.TryUndo(Grid);
            if (did) Changed?.Invoke();
            return did;
        }

        /// <summary>Redo the last undone action.</summary>
        public bool Redo()
        {
            bool did = _undo.TryRedo(Grid);
            if (did) Changed?.Invoke();
            return did;
        }

        /// <summary>Number of actions available to undo, for UI/diagnostics.</summary>
        public int UndoDepth => _undo.Depth;

        bool CommitPending()
        {
            int key = PendingKey;
            byte modifiers = PendingModifiers;

            // Validate BEFORE leaving Orienting: an invalid facing (e.g. one
            // that blocks its own exhaust/muzzle/fin clearance) must refuse
            // the commit and leave the pending placement in place, not
            // silently cancel it.
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

        /// <summary>Short human text for the HUD explaining why a hovered cell is invalid.</summary>
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

        /// <summary>
        /// Snap the direction from `from` to `to` onto the nearest of the four
        /// cardinal facings and encode it in the low 2 bits (0=+y,1=+x,2=-y,
        /// 3=-x), matching the Ship branch's modifier convention.
        /// </summary>
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
