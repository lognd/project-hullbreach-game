using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Net;
using Hullbreach.Ship;

namespace Hullbreach.Net.Tests
{
    public class HitToleranceTests
    {
        static ShipBody OneCellShip()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core, 0, 0));
            ship.RebuildDerivedViews();
            return ship;
        }

        [Test]
        public void Margin_ScalesWithSpeedAndGrace_AndIsCapped()
        {
            var tol = new HitTolerance(0.1f, 0.5f);
            Assert.AreEqual(0f, tol.MarginFor(0f), 1e-6f);
            Assert.AreEqual(0.2f, tol.MarginFor(2f), 1e-6f);
            Assert.AreEqual(0.5f, tol.MarginFor(1000f), 1e-6f, "cap");
            Assert.AreEqual(0f, HitTolerance.None.MarginFor(50f), 1e-6f);
        }

        [Test]
        public void Graze_InsideMargin_FavorsDefender_ButCentreHitsAlwaysCount()
        {
            var ship = OneCellShip();
            // Circle of radius 0.25 centred 0.2 outside the cell overlaps by 0.05.
            var graze = new float2(1.2f, 0.5f);
            Assert.IsTrue(ProjectileHitTest.TryHitShipCell(ship, graze, 0.25f, 0f, out _), "no margin: hit");
            Assert.IsFalse(ProjectileHitTest.TryHitShipCell(ship, graze, 0.25f, 0.1f, out _), "forgiven graze");
            Assert.IsTrue(ProjectileHitTest.TryHitShipCell(ship, graze, 0.25f, 0.04f, out _), "margin below overlap");
            Assert.IsTrue(ProjectileHitTest.TryHitShipCell(ship, new float2(0.5f, 0.5f), 0.25f, 10f, out _),
                "a shot centred inside the cell is never forgiven");
        }

        [Test]
        public void NegativeMargin_IsTreatedAsZero()
        {
            var ship = OneCellShip();
            Assert.IsTrue(ProjectileHitTest.TryHitShipCell(ship, new float2(1.2f, 0.5f), 0.25f, -1f, out _));
        }
    }
}
