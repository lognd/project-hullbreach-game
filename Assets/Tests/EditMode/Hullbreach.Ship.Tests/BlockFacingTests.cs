using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Ship;

namespace Hullbreach.Ship.Tests
{
    // Locks down the modifier-bits-to-direction encoding that thrust
    // application, muzzle direction and gizmos all share.
    public class BlockFacingTests
    {
        [Test]
        public void MapsAllFourFacingsToUnitVectors()
        {
            AssertUnit(new float2(0f, 1f), BlockFacing.FromModifiers(0));
            AssertUnit(new float2(1f, 0f), BlockFacing.FromModifiers(1));
            AssertUnit(new float2(0f, -1f), BlockFacing.FromModifiers(2));
            AssertUnit(new float2(-1f, 0f), BlockFacing.FromModifiers(3));
        }

        [Test]
        public void OnlyTheLowTwoBitsAreConsulted()
        {
            // Higher bits are reserved for other upgrades; they must not
            // perturb the facing.
            Assert.AreEqual(BlockFacing.FromModifiers(0), BlockFacing.FromModifiers(0b1111_1100));
            Assert.AreEqual(BlockFacing.FromModifiers(1), BlockFacing.FromModifiers(0b1010_1101));
        }

        static void AssertUnit(float2 expected, float2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-6f);
            Assert.AreEqual(expected.y, actual.y, 1e-6f);
            Assert.AreEqual(1f, math.length(actual), 1e-6f);
        }
    }
}
