using System.Collections.Generic;
using Hullbreach.Core;

namespace Hullbreach.Builder
{
    /// <summary>
    /// Undo/redo for the builder. S31 requires at least ten deep; capped at
    /// <see cref="MaxDepth"/> so a long session cannot grow the history
    /// unbounded, dropping the oldest action once full.
    ///
    /// Each entry is a whole ACTION, not a single block: a placement is one
    /// key+block, but a detach removal can strand several blocks, and all of
    /// those must undo (and redo) together as one step, matching the single
    /// click that caused them.
    /// </summary>
    public sealed class UndoStack
    {
        /// <summary>S31's minimum required undo depth.</summary>
        public const int MinimumDepth = 10;

        /// <summary>Cap on recorded actions; comfortably above MinimumDepth.</summary>
        public const int MaxDepth = 64;

        /// <summary>One (key, block) pair as it existed in the grid before the action removed it, or as placed.</summary>
        struct Entry
        {
            public int Key;
            public Block Block;
        }

        enum Kind { Place, Remove }

        /// <summary>
        /// One undoable action. A Place action has exactly one entry (the
        /// block that was added). A Remove action has one or more entries
        /// (the requested block plus any detached by the detach rule).
        /// </summary>
        sealed class Action
        {
            public Kind Kind;
            public List<Entry> Entries = new List<Entry>();
        }

        readonly List<Action> _undoStack = new List<Action>();
        readonly List<Action> _redoStack = new List<Action>();

        /// <summary>Number of actions currently available to undo.</summary>
        public int Depth => _undoStack.Count;

        /// <summary>
        /// Record a single-block placement. Call AFTER the block is already
        /// in the grid. Clears the redo stack, since a new action makes the
        /// previously-undone future unreachable.
        /// </summary>
        public void RecordPlace(int key, Block placed)
        {
            var action = new Action { Kind = Kind.Place };
            action.Entries.Add(new Entry { Key = key, Block = placed });
            Push(action);
        }

        /// <summary>
        /// Record a removal (possibly a detach batch) as one action. `removed`
        /// pairs each removed key with the block that occupied it BEFORE
        /// removal, so undo can restore type, modifiers AND damage exactly.
        /// Call AFTER the blocks are already removed from the grid.
        /// </summary>
        public void RecordRemove(IReadOnlyList<(int Key, Block Block)> removed)
        {
            var action = new Action { Kind = Kind.Remove };
            foreach (var (key, block) in removed)
            {
                action.Entries.Add(new Entry { Key = key, Block = block });
            }
            Push(action);
        }

        void Push(Action action)
        {
            _undoStack.Add(action);
            if (_undoStack.Count > MaxDepth)
            {
                // Drop the oldest to respect the cap; index 0 is the oldest
                // since actions are appended at the end.
                _undoStack.RemoveAt(0);
            }
            // A fresh action invalidates whatever redo history existed.
            _redoStack.Clear();
        }

        /// <summary>
        /// Reverse the last action against `grid`: a Place is undone by
        /// removing its block, a Remove (including a detach batch) is undone
        /// by restoring every entry exactly. Returns false when there is
        /// nothing to undo.
        /// </summary>
        public bool TryUndo(BlockGrid grid)
        {
            if (_undoStack.Count == 0) return false;

            var action = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            Apply(grid, action, reverse: true);

            _redoStack.Add(action);
            return true;
        }

        /// <summary>
        /// Re-apply the most recently undone action. Returns false when there
        /// is nothing to redo.
        /// </summary>
        public bool TryRedo(BlockGrid grid)
        {
            if (_redoStack.Count == 0) return false;

            var action = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);

            Apply(grid, action, reverse: false);

            _undoStack.Add(action);
            return true;
        }

        /// <summary>
        /// Apply an action's forward effect (reverse=false, i.e. redo) or its
        /// inverse (reverse=true, i.e. undo). A Place forward adds; its
        /// inverse removes. A Remove forward removes; its inverse restores.
        /// </summary>
        static void Apply(BlockGrid grid, Action action, bool reverse)
        {
            // A Place is added on redo and removed on undo; a Remove is
            // removed on redo and restored (re-added) on undo.
            bool shouldAdd = (action.Kind == Kind.Place && !reverse)
                           || (action.Kind == Kind.Remove && reverse);

            if (shouldAdd)
            {
                foreach (var entry in action.Entries)
                {
                    grid.TryAdd(entry.Key, entry.Block);
                }
            }
            else
            {
                foreach (var entry in action.Entries)
                {
                    grid.TryRemove(entry.Key);
                }
            }
        }
    }
}
