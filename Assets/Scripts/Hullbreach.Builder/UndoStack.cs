using System.Collections.Generic;
using Hullbreach.Core;

namespace Hullbreach.Builder
{
    // Undo/redo for the builder, one whole ACTION (not one block) per
    // entry; see docs/reference/hullbreach-builder.md#undostack.
    // frob:doc docs/reference/hullbreach-builder.md#undostack
    public sealed class UndoStack
    {
        // S31's minimum required undo depth.
        // frob:doc docs/reference/hullbreach-builder.md#undostack
        public const int MinimumDepth = 10;

        // Comfortably above MinimumDepth.
        // frob:doc docs/reference/hullbreach-builder.md#undostack
        public const int MaxDepth = 64;

        // One (key, block) pair as it existed before removal, or as placed.
        struct Entry
        {
            public int Key;
            public Block Block;
        }

        enum Kind { Place, Remove }

        // A Place has one entry; a Remove has one or more (detach batch).
        sealed class Action
        {
            public Kind Kind;
            public List<Entry> Entries = new List<Entry>();
        }

        readonly List<Action> _undoStack = new List<Action>();
        readonly List<Action> _redoStack = new List<Action>();

        // frob:doc docs/reference/hullbreach-builder.md#undostack
        public int Depth => _undoStack.Count;

        // Call AFTER the block is already in the grid; clears redo.
        // frob:doc docs/reference/hullbreach-builder.md#undostack
        public void RecordPlace(int key, Block placed)
        {
            var action = new Action { Kind = Kind.Place };
            action.Entries.Add(new Entry { Key = key, Block = placed });
            Push(action);
        }

        // `removed` pairs each key with its block BEFORE removal, for exact
        // restore. Call AFTER the blocks are already removed.
        // frob:doc docs/reference/hullbreach-builder.md#undostack
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
                // Drop the oldest; index 0, since actions append at the end.
                _undoStack.RemoveAt(0);
            }
            // A fresh action invalidates whatever redo history existed.
            _redoStack.Clear();
        }

        // Reverses the last action; false when there is nothing to undo.
        // frob:doc docs/reference/hullbreach-builder.md#undostack
        public bool TryUndo(BlockGrid grid)
        {
            if (_undoStack.Count == 0) return false;

            var action = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            Apply(grid, action, reverse: true);

            _redoStack.Add(action);
            return true;
        }

        // Re-applies the most recently undone action.
        // frob:doc docs/reference/hullbreach-builder.md#undostack
        public bool TryRedo(BlockGrid grid)
        {
            if (_redoStack.Count == 0) return false;

            var action = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);

            Apply(grid, action, reverse: false);

            _undoStack.Add(action);
            return true;
        }

        // Forward (redo) or inverse (undo) effect of one action.
        static void Apply(BlockGrid grid, Action action, bool reverse)
        {
            // A Place is added on redo and removed on undo; a Remove is
            // removed on redo and restored on undo.
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
