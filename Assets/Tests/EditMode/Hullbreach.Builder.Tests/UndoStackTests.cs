using System.Collections.Generic;
using NUnit.Framework;
using Hullbreach.Core;
using Hullbreach.Builder;

namespace Hullbreach.Builder.Tests
{
    public class UndoStackTests
    {
        [Test]
        public void Undo_TenDeep_RestoresExactBlocksIncludingDamageAndModifiers()
        {
            var g = new BlockGrid();
            var undo = new UndoStack();

            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            undo.RecordPlace(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));

            // Place ten more blocks, some with damage/modifiers, each as its
            // own undoable action.
            for (int i = 1; i <= 10; i++)
            {
                var block = new Block(BlockTypes.Hull, modifiers: (byte)(i % 4), damage: (byte)(i * 10));
                int key = BlockKey.Pack(i, 0);
                g.TryAdd(key, block);
                undo.RecordPlace(key, block);
            }

            Assert.AreEqual(11, undo.Depth);
            Assert.AreEqual(11, g.Count);

            // Undo all ten placements (not the core) and check every block
            // is gone; then redo and check every block is back exactly.
            var expected = new Dictionary<int, Block>();
            for (int i = 1; i <= 10; i++)
            {
                expected[BlockKey.Pack(i, 0)] = new Block(BlockTypes.Hull, modifiers: (byte)(i % 4), damage: (byte)(i * 10));
            }

            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(undo.TryUndo(g));
            }
            Assert.AreEqual(1, g.Count, "only the core should remain after undoing all ten");

            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(undo.TryRedo(g));
            }
            Assert.AreEqual(11, g.Count);

            foreach (var kvp in expected)
            {
                Assert.IsTrue(g.TryGet(kvp.Key, out var block));
                Assert.AreEqual(kvp.Value.TypeId, block.TypeId);
                Assert.AreEqual(kvp.Value.Modifiers, block.Modifiers);
                Assert.AreEqual(kvp.Value.Damage, block.Damage);
            }
        }

        [Test]
        public void UndoOfDetach_RestoresAllStrandedBlocksAsOneAction()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull, modifiers: 2));
            g.TryAdd(BlockKey.Pack(2, 0), new Block(BlockTypes.Hull, damage: 40));

            var undo = new UndoStack();
            var removed = new List<int>();
            Assert.IsTrue(PlacementRules.Detach(g, BlockKey.Pack(1, 0), removed));
            Assert.AreEqual(2, removed.Count);
            Assert.AreEqual(1, g.Count);

            var entries = new List<(int Key, Block Block)>
            {
                (BlockKey.Pack(1, 0), new Block(BlockTypes.Hull, modifiers: 2)),
                (BlockKey.Pack(2, 0), new Block(BlockTypes.Hull, damage: 40)),
            };
            undo.RecordRemove(entries);

            Assert.AreEqual(1, undo.Depth, "a detach batch is one action");

            Assert.IsTrue(undo.TryUndo(g));
            Assert.AreEqual(3, g.Count, "undo must restore every stranded block");

            Assert.IsTrue(g.TryGet(BlockKey.Pack(1, 0), out var restored1));
            Assert.AreEqual((byte)2, restored1.Modifiers);

            Assert.IsTrue(g.TryGet(BlockKey.Pack(2, 0), out var restored2));
            Assert.AreEqual((byte)40, restored2.Damage);
        }

        [Test]
        public void Cap_DropsOldestAction()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));

            var undo = new UndoStack();
            for (int i = 1; i <= UndoStack.MaxDepth + 5; i++)
            {
                int key = BlockKey.Pack(i, 0);
                var block = new Block(BlockTypes.Hull);
                g.TryAdd(key, block);
                undo.RecordPlace(key, block);
            }

            Assert.AreEqual(UndoStack.MaxDepth, undo.Depth, "stack must not exceed the cap");
        }

        [Test]
        public void TryUndo_OnEmptyStack_ReturnsFalse()
        {
            var g = new BlockGrid();
            var undo = new UndoStack();
            Assert.IsFalse(undo.TryUndo(g));
        }

        [Test]
        public void TryRedo_WithNoUndoneAction_ReturnsFalse()
        {
            var g = new BlockGrid();
            var undo = new UndoStack();
            Assert.IsFalse(undo.TryRedo(g));
        }

        [Test]
        public void NewAction_ClearsRedoHistory()
        {
            var g = new BlockGrid();
            var undo = new UndoStack();

            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            undo.RecordPlace(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));

            g.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull));
            undo.RecordPlace(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull));

            Assert.IsTrue(undo.TryUndo(g)); // undo the hull placement

            g.TryAdd(BlockKey.Pack(2, 0), new Block(BlockTypes.Hull));
            undo.RecordPlace(BlockKey.Pack(2, 0), new Block(BlockTypes.Hull)); // a new action

            Assert.IsFalse(undo.TryRedo(g), "recording a new action must drop the stale redo entry");
        }
    }
}
