using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Ship;

namespace Hullbreach.Ship.Tests
{
    // S30 criterion 3: a block placed through the builder is attached to
    // the hull and moves with the ship. Literal reading: it stays in the
    // grid, stays connected to the core, and keeps a fixed ship-local
    // offset while ShipBody translates and rotates.
    // frob:ticket 01M3DG5Y0KYH80B8DZANENR2DM
    public class BuilderAttachedBlockTests
    {
        static readonly int CoreKey = BlockKey.Pack(0, 0);
        static readonly int ThrusterKey = BlockKey.Pack(1, 0);
        static readonly int HullKey = BlockKey.Pack(0, 1);

        static ShipBody BuildShip()
        {
            var ship = new ShipBody();
            var session = new BuilderSession(ship.Grid);
            session.Select(BlockTypes.Core);
            session.Click(CoreKey);
            session.Select(BlockTypes.Thruster);
            session.Click(ThrusterKey);
            session.Select(BlockTypes.Hull);
            session.Click(HullKey);
            return ship;
        }

        // frob:tests Hullbreach.Builder.BuilderSession
        [Test]
        public void PlacedBlock_IsAttachedToTheCore()
        {
            var ship = BuildShip();

            Assert.IsTrue(ship.Grid.Contains(HullKey));
            var detached = new List<int>();
            Connectivity.FindDetached(ship.Grid, detached);
            Assert.IsEmpty(detached, "a block the builder accepted must be connected to the core");
        }

        // frob:tests Hullbreach.Ship.ShipBody
        [Test]
        public void PlacedBlock_MovesAndRotatesWithTheShip()
        {
            var ship = BuildShip();
            float2 localCenter = BlockGrid.CenterOf(HullKey);
            float2 coreLocal = BlockGrid.CenterOf(CoreKey);
            float startDistance = math.distance(localCenter, coreLocal);

            for (int i = 0; i < 120; i++)
                ship.Step(new ShipInput(1f, 1f, false), 1f / 60f);

            Assert.Greater(math.length(ship.Position), 0.01f, "the ship must have translated");
            Assert.AreNotEqual(0f, ship.Rotation, "the ship must have rotated");

            float2 hullWorld = ship.LocalToWorld(localCenter);
            float2 coreWorld = ship.LocalToWorld(coreLocal);
            Assert.AreEqual(startDistance, math.distance(hullWorld, coreWorld), 1e-3f,
                "rigid body: the block keeps its distance to the core");
            float2 back = ship.WorldToLocal(hullWorld);
            Assert.AreEqual(localCenter.x, back.x, 1e-3f);
            Assert.AreEqual(localCenter.y, back.y, 1e-3f);
            Assert.IsTrue(ship.Grid.Contains(HullKey), "still part of the simulated grid");
        }
    }
}
