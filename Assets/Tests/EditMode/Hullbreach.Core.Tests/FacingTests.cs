using NUnit.Framework;
using Hullbreach.Core;

namespace Hullbreach.Core.Tests
{
    public class FacingTests
    {
        const float Tol = 1e-5f;

        [Test]
        public void Step_MatchesTheFourCardinalDirections()
        {
            Facing.Step(0, out int dx0, out int dy0);
            Assert.AreEqual(0, dx0); Assert.AreEqual(1, dy0);

            Facing.Step(1, out int dx1, out int dy1);
            Assert.AreEqual(1, dx1); Assert.AreEqual(0, dy1);

            Facing.Step(2, out int dx2, out int dy2);
            Assert.AreEqual(0, dx2); Assert.AreEqual(-1, dy2);

            Facing.Step(3, out int dx3, out int dy3);
            Assert.AreEqual(-1, dx3); Assert.AreEqual(0, dy3);
        }

        [Test]
        public void Opposite_ReversesTheFacingAndKeepsHighBits()
        {
            Assert.AreEqual(2, Facing.Opposite(0));
            Assert.AreEqual(3, Facing.Opposite(1));
            Assert.AreEqual(0, Facing.Opposite(2));
            Assert.AreEqual(1, Facing.Opposite(3));

            // High bits (upgrade flags outside the facing mask) survive.
            byte withHighBits = 0b0001_0000 | 1;
            Assert.AreEqual(0b0001_0000 | 3, Facing.Opposite(withHighBits));
        }

        [Test]
        public void Perpendicular_IsDirectionRotated90DegreesCcw()
        {
            var perp = Facing.Perpendicular(0);
            Assert.AreEqual(-1f, perp.x, Tol);
            Assert.AreEqual(0f, perp.y, Tol);

            var perp1 = Facing.Perpendicular(1);
            Assert.AreEqual(0f, perp1.x, Tol);
            Assert.AreEqual(1f, perp1.y, Tol);
        }

        [Test]
        public void Ahead_StepsOneCellInTheFacingDirection()
        {
            int key = BlockKey.Pack(0, 0);
            int ahead = Facing.Ahead(key, 0);

            BlockKey.Unpack(ahead, out int x, out int y);
            Assert.AreEqual(0, x);
            Assert.AreEqual(1, y);
        }

        [Test]
        public void Behind_StepsOneCellOppositeTheFacingDirection()
        {
            int key = BlockKey.Pack(0, 0);
            int behind = Facing.Behind(key, 0);

            BlockKey.Unpack(behind, out int x, out int y);
            Assert.AreEqual(0, x);
            Assert.AreEqual(-1, y);
        }

        [Test]
        public void Ahead_ReturnsMinusOneWhenOutOfRange()
        {
            int key = BlockKey.Pack(BlockKey.Max, 0);
            Assert.AreEqual(-1, Facing.Ahead(key, 1));
        }

        [Test]
        public void Behind_ReturnsMinusOneWhenOutOfRange()
        {
            int key = BlockKey.Pack(BlockKey.Min, 0);
            Assert.AreEqual(-1, Facing.Behind(key, 1));
        }
    }
}
