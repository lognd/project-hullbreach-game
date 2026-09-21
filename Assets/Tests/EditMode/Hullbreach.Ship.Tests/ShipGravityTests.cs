using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;
using Hullbreach.World;

namespace Hullbreach.Ship.Tests
{
    /// <summary>Covers ShipBody's gravity integration: per-block body force
    /// (linear a = g, tidal torque off-axis) and planet surface contact
    /// (push-out, restitution, contact damage).</summary>
    public class ShipGravityTests
    {
        static GravityField MakeField(float mu = 400f, float radius = 1f)
        {
            var field = new GravityField();
            field.Add(new GravityBody(float2.zero, mu, radius, surfaceRestitution: 0.5f));
            return field;
        }

        [Test]
        public void SingleBlockShip_LinearAcceleration_MatchesFieldWithinOnePercent()
        {
            var field = MakeField();
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core, 0));
            ship.RebuildDerivedViews();
            ship.Gravity = field;
            ship.Position = new float2(50f, 0f);

            // The single block's own world-space center (not ship.Position
            // itself, which is offset from it by half a block) is what
            // gravity is actually evaluated at.
            float2 blockWorldCenter = ship.LocalToWorld(BlockGrid.CenterOf(BlockKey.Pack(0, 0)));

            ship.Step(default, 1f / 60f);

            float2 expected = field.AccelerationAt(blockWorldCenter);
            float error = math.length(ship.LastLinearAcceleration - expected) / math.length(expected);
            Assert.LessOrEqual(error, 0.01f);
        }

        [Test]
        public void TwoBlockShip_OffAxisInStrongGradient_RecordsNonzeroTidalTorque()
        {
            // A strong (small-radius, large-mu) body puts the two blocks at
            // very different distances when the ship sits off-axis, so the
            // near block is pulled noticeably harder than the far one.
            var field = MakeField(mu: 4000f, radius: 0.5f);

            var offAxis = new ShipBody();
            offAxis.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core, 0));
            offAxis.Grid.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull, 0));
            offAxis.RebuildDerivedViews();
            offAxis.Gravity = field;
            // Positioned so the line between blocks is NOT radial to the body.
            offAxis.Position = new float2(3f, 3f);

            offAxis.Step(default, 1f / 60f);
            Assert.AreNotEqual(0f, offAxis.LastAngularAcceleration);

            // Same two blocks, but oriented so the line between them IS
            // radial (straight out from the body): no tidal torque. Both
            // block centers land exactly on y=0 (a line straight through
            // the body at the origin) once the -0.5 offset (block centers
            // sit at local y=0.5) is folded into Position.
            var onAxis = new ShipBody();
            onAxis.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core, 0));
            onAxis.Grid.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull, 0));
            onAxis.RebuildDerivedViews();
            onAxis.Gravity = field;
            onAxis.Position = new float2(3f, -0.5f);

            onAxis.Step(default, 1f / 60f);
            Assert.AreEqual(0f, onAxis.LastAngularAcceleration, 1e-3f);
        }

        [Test]
        public void ShipFallingOntoPlanet_EndsUpOutsideSurface_WithNonNegativeNormalVelocity()
        {
            var field = MakeField(mu: 400f, radius: 5f);
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core, 0));
            ship.RebuildDerivedViews();
            ship.Gravity = field;

            ship.Position = new float2(0f, 5.4f); // just above the surface + clearance
            ship.Velocity = new float2(0f, -50f); // falling fast, straight down

            for (int i = 0; i < 30; i++)
            {
                ship.Step(default, 1f / 60f);
            }

            float distanceFromCenter = math.length(ship.Position);
            Assert.GreaterOrEqual(distanceFromCenter, 5f - 1e-3f);

            // After Step's contact resolution, the block's velocity along
            // the outward normal must not still be driving it INTO the
            // planet.
            float2 normal = math.normalize(ship.Position);
            float normalVelocity = math.dot(ship.Velocity, normal);
            Assert.GreaterOrEqual(normalVelocity, -1e-2f);
        }

        [Test]
        public void FastImpact_DamagesTheContactingBlock()
        {
            var field = MakeField(mu: 400f, radius: 5f);
            var ship = new ShipBody();
            int key = BlockKey.Pack(0, 0);
            ship.Grid.TryAdd(key, new Block(BlockTypes.Core, 0));
            ship.RebuildDerivedViews();
            ship.Gravity = field;
            ship.ContactDamageSpeed = 3f;
            ship.ContactDamagePerSpeed = 20f;

            ship.Position = new float2(0f, 5.4f);
            ship.Velocity = new float2(0f, -100f); // a hard, fast impact

            ship.Step(default, 1f / 60f);

            ship.Grid.TryGet(key, out var block);
            Assert.Greater(block.Damage, 0);
        }
    }
}
