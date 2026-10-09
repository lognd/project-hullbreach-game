using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Structure;

namespace Hullbreach.Structure.Tests
{
    // Rebuild-trigger and stale-result coverage for StructuralSolver.Tick.
    public class StructuralSolverRebuildTests
    {
        const float EndForce = 0.02f;
        const int Height = 12;

        static BlockGrid Column()
        {
            var grid = new BlockGrid();
            grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            for (int y = 1; y < Height; y++)
                grid.TryAdd(BlockKey.Pack(0, y), new Block(BlockTypes.Hull));
            return grid;
        }

        static List<(float2, float2)> Forces(float magnitude)
        {
            return new List<(float2, float2)>
            {
                (new float2(0.05f, Height - 0.02f), new float2(0f, -magnitude)),
                (new float2(0.95f, Height - 0.02f), new float2(0f, -magnitude)),
                (new float2(0.05f, 0.02f), new float2(0f, magnitude)),
                (new float2(0.95f, 0.02f), new float2(0f, magnitude)),
            };
        }

        // frob:tests Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.Tick
        [Test]
        public void Damage_TriggersStiffnessRebuild()
        {
            var grid = Column();
            var solver = new StructuralSolver { BucklingEnabled = false };
            var forces = Forces(EndForce);
            // ClearDirty per tick like ShipBody, else TopologyDirty alone forces rebuilds.
            for (int i = 0; i < 20; i++) { solver.Tick(grid, forces, 1f / 60f); grid.ClearDirty(); }
            Assert.IsTrue(solver.ContinuedFromLastTick, "steady load should continue the Krylov state");

            int key = BlockKey.Pack(0, 5);
            Assert.IsTrue(grid.TryGet(key, out Block b));
            grid.TrySet(key, b.WithDamage(200));
            solver.Tick(grid, forces, 1f / 60f);

            Assert.IsFalse(solver.ContinuedFromLastTick, "damage changes K, so the solve must restart");
        }

        // frob:tests Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.Tick
        [Test]
        public void RemovedBlock_IsNotListedAsBuckled()
        {
            var grid = Column();
            var solver = new StructuralSolver { BucklingEveryNTicks = 1, BucklingMaxSweepsPerTick = 8, BucklingModeCount = 2 };
            var forces = Forces(EndForce * 50f);
            for (int i = 0; i < 60 && solver.BuckledBlocks.Count == 0; i++)
                solver.Tick(grid, forces, 1f / 60f);
            Assert.Greater(solver.BuckledBlocks.Count, 0, "precondition: the column must have buckled blocks");

            // Remove the block at the free end, then tick with buckling
            // throttled off so only the stale-result path is exercised.
            int top = BlockKey.Pack(0, Height - 1);
            Assert.IsTrue(grid.TryRemove(top));
            solver.BucklingEveryNTicks = 1000;
            solver.Tick(grid, forces, 1f / 60f);

            foreach (int key in solver.BuckledBlocks)
                Assert.IsTrue(grid.Contains(key), "BuckledBlocks must only list live blocks");
            foreach (var mode in solver.BucklingModes)
                foreach (int key in mode.BlockParticipation.Keys)
                    Assert.IsTrue(grid.Contains(key), "mode participation must only list live blocks");
        }
    }
}
