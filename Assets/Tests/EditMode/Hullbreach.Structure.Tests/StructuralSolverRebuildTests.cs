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

        // A block swap keeps the DOF count (5 nodes leave, 5 arrive) but
        // allocates a new CSR pattern, so K_G has to follow it.
        // frob:tests Assets/Scripts/Hullbreach.Structure/StructuralSolver.cs::StructuralSolver.Tick
        [Test]
        public void SwappedBlock_SameDofCount_ReattachesGeometricStiffness()
        {
            var grid = Column();
            var solver = new StructuralSolver { BucklingEveryNTicks = 1, BucklingMaxSweepsPerTick = 8, BucklingModeCount = 2 };
            var forces = CompressionAt(Height - 1, EndForce * 50f);
            for (int i = 0; i < 60 && solver.BucklingModes.Count == 0; i++)
            {
                solver.Tick(grid, forces, 1f / 60f);
                grid.ClearDirty();
            }
            Assert.Greater(solver.BucklingModes.Count, 0, "precondition: K_G must be attached and in use");

            Assert.IsTrue(grid.TryRemove(BlockKey.Pack(0, Height - 1)));
            Assert.IsTrue(grid.TryAdd(BlockKey.Pack(1, Height - 2), new Block(BlockTypes.Hull)));

            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 40; i++)
                {
                    solver.Tick(grid, forces, 1f / 60f);
                    grid.ClearDirty();
                }
            });

            var fresh = new StructuralSolver { BucklingEveryNTicks = 1, BucklingMaxSweepsPerTick = 8, BucklingModeCount = 2 };
            for (int i = 0; i < 40; i++)
            {
                fresh.Tick(grid, forces, 1f / 60f);
                grid.ClearDirty();
            }
            Assert.AreEqual(fresh.BlockStresses.Count, solver.BlockStresses.Count);
            foreach (var kv in fresh.BlockStresses)
            {
                Assert.IsTrue(solver.BlockStresses.TryGetValue(kv.Key, out var got), "block missing from stress map");
                float scale = System.Math.Max(1f, System.Math.Abs(kv.Value.Syy));
                Assert.AreEqual(kv.Value.Sxx, got.Sxx, 0.02f * scale);
                Assert.AreEqual(kv.Value.Syy, got.Syy, 0.02f * scale);
                Assert.AreEqual(kv.Value.Txy, got.Txy, 0.02f * scale);
            }
        }

        static List<(float2, float2)> CompressionAt(int topRow, float magnitude)
        {
            return new List<(float2, float2)>
            {
                (new float2(0.05f, topRow + 1 - 0.02f), new float2(0f, -magnitude)),
                (new float2(0.95f, topRow + 1 - 0.02f), new float2(0f, -magnitude)),
                (new float2(0.05f, 0.02f), new float2(0f, magnitude)),
                (new float2(0.95f, 0.02f), new float2(0f, magnitude)),
            };
        }
    }
}
