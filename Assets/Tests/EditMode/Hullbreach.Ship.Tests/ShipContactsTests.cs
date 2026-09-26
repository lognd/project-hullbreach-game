using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;

namespace Hullbreach.Ship.Tests
{
    // Ship-to-ship contact resolution. These cover the failure the player
    // saw: flying into the target ship flung it off the map, because two
    // Box2D dynamic bodies were being overwritten by ShipBody every tick and
    // the depenetration solver fought back.
    public sealed class ShipContactsTests
    {
        static ShipBody SingleBlockShip(float2 position, float2 velocity)
        {
            var ship = new ShipBody { Position = position, Velocity = velocity };
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core, 0));
            ship.RebuildDerivedViews();
            return ship;
        }

        [Test]
        public void OverlappingShips_SeparateAlongTheNormal()
        {
            // Half a block apart on x: deeply overlapping.
            var a = SingleBlockShip(new float2(0f, 0f), float2.zero);
            var b = SingleBlockShip(new float2(0.5f, 0f), float2.zero);

            float gapBefore = math.distance(a.Position, b.Position);
            Assert.IsTrue(ShipContacts.Resolve(a, b, 0.02f), "overlapping ships reported no contact");

            float gapAfter = math.distance(a.Position, b.Position);
            Assert.Greater(gapAfter, gapBefore, "contact did not separate the ships");
            Assert.AreEqual(0f, a.Position.y, 1e-4f, "separation drifted off the contact normal");
            Assert.AreEqual(0f, b.Position.y, 1e-4f, "separation drifted off the contact normal");
            Assert.Less(a.Position.x, b.Position.x, "the ships swapped sides");
        }

        [Test]
        public void SeparationSplitsByInverseMass()
        {
            var light = SingleBlockShip(new float2(0f, 0f), float2.zero);

            // A three-block ship is heavier, so it should move less.
            var heavy = new ShipBody { Position = new float2(0.5f, 0f) };
            heavy.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core, 0));
            heavy.Grid.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Armor, 0));
            heavy.Grid.TryAdd(BlockKey.Pack(2, 0), new Block(BlockTypes.Armor, 0));
            heavy.RebuildDerivedViews();

            float2 lightBefore = light.Position;
            float2 heavyBefore = heavy.Position;
            ShipContacts.Resolve(light, heavy, 0.02f);

            float lightMoved = math.distance(light.Position, lightBefore);
            float heavyMoved = math.distance(heavy.Position, heavyBefore);
            Assert.Greater(lightMoved, heavyMoved, "the heavier ship was pushed further than the lighter one");
        }

        [Test]
        public void HeadOnCollision_ConservesMomentum()
        {
            var a = SingleBlockShip(new float2(-0.4f, 0f), new float2(6f, 0f));
            var b = SingleBlockShip(new float2(0.4f, 0f), new float2(-6f, 0f));

            float2 momentumBefore = a.Velocity * a.Grid.Mass.Total + b.Velocity * b.Grid.Mass.Total;
            ShipContacts.Resolve(a, b, 0.02f);
            float2 momentumAfter = a.Velocity * a.Grid.Mass.Total + b.Velocity * b.Grid.Mass.Total;

            Assert.AreEqual(momentumBefore.x, momentumAfter.x, 1e-3f, "linear momentum was not conserved");
            Assert.AreEqual(momentumBefore.y, momentumAfter.y, 1e-3f, "linear momentum was not conserved");
        }

        [Test]
        public void HeadOnCollision_PushesTheShipsApartAndStaysFinite()
        {
            var a = SingleBlockShip(new float2(-0.4f, 0f), new float2(6f, 0f));
            var b = SingleBlockShip(new float2(0.4f, 0f), new float2(-6f, 0f));

            ShipContacts.Resolve(a, b, 0.02f);

            Assert.Less(a.Velocity.x, 0f, "the left ship did not bounce back");
            Assert.Greater(b.Velocity.x, 0f, "the right ship did not bounce back");
            foreach (float value in new[] { a.Position.x, a.Position.y, a.Velocity.x, a.Velocity.y,
                                            b.Position.x, b.Position.y, b.Velocity.x, b.Velocity.y })
            {
                Assert.IsFalse(float.IsNaN(value) || float.IsInfinity(value), "contact produced a non-finite value");
            }

            // Nothing may be flung: the bounce speed is bounded by the
            // closing speed times (1 + restitution).
            float bound = 12f * (1f + ShipContacts.Restitution);
            Assert.Less(math.length(a.Velocity), bound, "the left ship was flung");
            Assert.Less(math.length(b.Velocity), bound, "the right ship was flung");
        }

        [Test]
        public void FastCollision_DamagesBothContactingBlocks()
        {
            var a = SingleBlockShip(new float2(-0.4f, 0f), new float2(30f, 0f));
            var b = SingleBlockShip(new float2(0.4f, 0f), new float2(-30f, 0f));

            ShipContacts.Resolve(a, b, 0.02f);

            Assert.IsTrue(a.Grid.TryGet(BlockKey.Pack(0, 0), out var blockA));
            Assert.IsTrue(b.Grid.TryGet(BlockKey.Pack(0, 0), out var blockB));
            Assert.Greater(blockA.Damage, 0, "the left ship's contacting block took no damage");
            Assert.Greater(blockB.Damage, 0, "the right ship's contacting block took no damage");
        }

        [Test]
        public void SlowContact_DoesNotDamage()
        {
            var a = SingleBlockShip(new float2(-0.45f, 0f), new float2(0.2f, 0f));
            var b = SingleBlockShip(new float2(0.45f, 0f), new float2(-0.2f, 0f));

            ShipContacts.Resolve(a, b, 0.02f);

            Assert.IsTrue(a.Grid.TryGet(BlockKey.Pack(0, 0), out var blockA));
            Assert.AreEqual(0, blockA.Damage, "a gentle nudge damaged a block");
        }

        [Test]
        public void DistantShips_ReportNoContact()
        {
            var a = SingleBlockShip(new float2(0f, 0f), float2.zero);
            var b = SingleBlockShip(new float2(40f, 0f), float2.zero);

            Assert.IsFalse(ShipContacts.Resolve(a, b, 0.02f), "ships 40 units apart reported a contact");
        }

        [Test]
        public void SameShipTwice_IsIgnored()
        {
            var a = SingleBlockShip(float2.zero, float2.zero);
            Assert.IsFalse(ShipContacts.Resolve(a, a, 0.02f), "a ship collided with itself");
        }
    }
}
