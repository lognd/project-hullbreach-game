using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Structure;

namespace Hullbreach.Structure.Tests
{
    // S36 criteria 1 and 2: where the stress lands under a thrust load, and that a brace lowers it.
    public class StressDistributionTests
    {
        const int ArmLength = 8;

        // A core block with an arm along +x, `rows` blocks thick.
        static BlockGrid Arm(int rows)
        {
            var grid = new BlockGrid();
            grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            for (int x = 1; x < ArmLength; x++)
            for (int y = 0; y < rows; y++)
                grid.TryAdd(BlockKey.Pack(x, y), new Block(BlockTypes.Hull));
            for (int y = 1; y < rows; y++)
                grid.TryAdd(BlockKey.Pack(0, y), new Block(BlockTypes.Hull));
            return grid;
        }

        // One sideways thrust at the arm tip: the load a tip thruster applies.
        static StructuralSolver Solve(BlockGrid grid)
        {
            var solver = new StructuralSolver();
            var forces = new List<(float2, float2)> { (new float2(ArmLength - 0.5f, 0.5f), new float2(0f, -20f)) };
            for (int i = 0; i < 5; i++) solver.Tick(grid, forces, 1f / 50f);
            return solver;
        }

        static float Ratio(StructuralSolver solver, int x, int y)
        {
            var s = solver.BlockStresses[BlockKey.Pack(x, y)];
            return System.Math.Max(s.DuctileRatio, s.BrittleRatio);
        }

        static float Peak(StructuralSolver solver)
        {
            float peak = 0f;
            foreach (var kv in solver.BlockStresses) peak = System.Math.Max(peak, System.Math.Max(kv.Value.DuctileRatio, kv.Value.BrittleRatio));
            return peak;
        }

        [Test]
        public void ThrustAtTheTip_StressesBlocksNearTheLoadMoreThanTheFreeEnd()
        {
            var solver = Solve(Arm(1));

            float nearLoad = Ratio(solver, ArmLength - 2, 0);
            float freeEnd = Ratio(solver, 0, 0);

            Assert.Greater(nearLoad, freeEnd);
        }

        [Test]
        public void AddingABraceRow_LowersThePeakStress()
        {
            float thin = Peak(Solve(Arm(1)));
            float braced = Peak(Solve(Arm(2)));

            // Measured about 10% lower (14.1 to 12.6 of yield); 5% leaves slack.
            Assert.Less(braced, thin * 0.95f);
        }
    }
}
