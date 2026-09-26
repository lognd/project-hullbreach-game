using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;
using Hullbreach.Ship.Behaviours;
using Hullbreach.World;

namespace Hullbreach.Ship.Tests
{
    // Covers the block behaviour extension point: registry
    // resolution/fallback, the gravity-gun/anti-gravity-gun shot payload,
    // the seeking thruster's targeting, and the temporary variant
    // (powerup) transform on ShipBody.
    public class BlockBehaviourTests
    {
        const float Tol = 1e-3f;

        [SetUp]
        public void RestoreDefaults() => BehaviourRegistry.RegisterDefaults();

        [Test]
        public void Resolve_UnregisteredVariant_FallsBackToTypeBase()
        {
            var block = new Block(BlockTypes.Cannon, BlockVariants.With(0, 9));
            var resolved = BehaviourRegistry.Resolve(block);
            Assert.IsInstanceOf<CannonBehaviour>(resolved,
                "an unregistered variant id must fall back to that type's variant 0");
        }

        [Test]
        public void Resolve_PlainBlockType_HasNoBehaviour()
        {
            Assert.IsNull(BehaviourRegistry.Resolve(new Block(BlockTypes.Hull)));
        }

        [Test]
        public void Resolve_KnownVariants_ReturnExpectedBehaviours()
        {
            Assert.IsInstanceOf<GravityGunBehaviour>(
                BehaviourRegistry.Resolve(new Block(BlockTypes.Cannon, BlockVariants.With(0, 1))));
            Assert.IsInstanceOf<AntiGravityGunBehaviour>(
                BehaviourRegistry.Resolve(new Block(BlockTypes.Cannon, BlockVariants.With(0, 2))));
            Assert.IsInstanceOf<SeekingThrusterBehaviour>(
                BehaviourRegistry.Resolve(new Block(BlockTypes.Thruster, BlockVariants.With(0, 1))));
        }

        static ShipBody CannonShip(byte variant)
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            ship.Grid.TryAdd(BlockKey.Pack(0, 1), new Block(BlockTypes.Cannon, BlockVariants.With(0, variant)));
            return ship;
        }

        [Test]
        public void GravityGun_Shot_CarriesPositiveWellSpec()
        {
            var ship = CannonShip(1);
            ship.Step(new ShipInput(0f, 0f, true), 1f / 60f);

            Assert.AreEqual(1, ship.PendingShots.Count);
            var shot = ship.PendingShots[0];
            Assert.AreEqual(ProjectileKind.GravityWell, shot.Spec.Kind);
            Assert.Greater(shot.Spec.Well.Mu, 0f, "the gravity gun attracts");
        }

        [Test]
        public void AntiGravityGun_Shot_CarriesNegativeWellSpec()
        {
            var ship = CannonShip(2);
            ship.Step(new ShipInput(0f, 0f, true), 1f / 60f);

            Assert.AreEqual(1, ship.PendingShots.Count);
            var shot = ship.PendingShots[0];
            Assert.AreEqual(ProjectileKind.GravityWell, shot.Spec.Kind);
            Assert.Less(shot.Spec.Well.Mu, 0f, "the anti-gravity gun repels");
        }

        [Test]
        public void PlainCannon_Shot_CarriesNoWellSpec()
        {
            var ship = CannonShip(0);
            ship.Step(new ShipInput(0f, 0f, true), 1f / 60f);

            Assert.AreEqual(1, ship.PendingShots.Count);
            Assert.AreEqual(ProjectileKind.None, ship.PendingShots[0].Spec.Kind);
        }

        // A stub sink whose TryNearestEnemy always reports a fixed
        // position, so the seeking thruster's direction can be asserted
        // exactly instead of depending on a second real ShipBody.
        sealed class StubSink : IWorldSink
        {
            public float2 EnemyPosition;
            public bool HasEnemy = true;

            public void SpawnProjectile(in ShotRequest shot) { }
            public void AddTemporaryGravity(GravityBody body, float seconds) { }
            public IReadOnlyList<ShipBody> Ships => System.Array.Empty<ShipBody>();

            public bool TryNearestEnemy(float2 from, ShipBody self, out float2 position, out float2 velocity)
            {
                position = EnemyPosition;
                velocity = float2.zero;
                return HasEnemy;
            }
        }

        [Test]
        public void SeekingThruster_PullsTowardNearestEnemy()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            ship.Grid.TryAdd(BlockKey.Pack(0, 1), new Block(BlockTypes.Thruster, BlockVariants.With(0, 1)));
            ship.World = new StubSink { EnemyPosition = new float2(10f, 0f) };

            // Ramp throttle up first (level 0 ramps over 1 second).
            for (int i = 0; i < 60; i++) ship.Step(new ShipInput(1f, 0f, false), 1f / 60f);
            ship.Step(new ShipInput(1f, 0f, false), 1f / 60f);

            Assert.Greater(ship.LastLinearAcceleration.x, 0f,
                "with the enemy due +x of the ship, the seeking thruster should pull the ship toward +x");
        }

        [Test]
        public void SeekingThruster_NoEnemy_FallsBackToShipForward()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            ship.Grid.TryAdd(BlockKey.Pack(0, 1), new Block(BlockTypes.Thruster, BlockVariants.With(0, 1)));
            ship.World = new StubSink { HasEnemy = false };

            for (int i = 0; i < 60; i++) ship.Step(new ShipInput(1f, 0f, false), 1f / 60f);

            Assert.Greater(ship.LastLinearAcceleration.y, 0f,
                "with no known enemy the seeking thruster must fall back to ordinary ship-forward thrust");
            Assert.AreEqual(0f, ship.LastLinearAcceleration.x, Tol);
        }

        [Test]
        public void ApplyPowerup_PicksNearestCannon_AndExpiresBackToBase()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            int nearKey = BlockKey.Pack(0, 1);
            int farKey = BlockKey.Pack(0, 5);
            ship.Grid.TryAdd(nearKey, new Block(BlockTypes.Cannon));
            ship.Grid.TryAdd(farKey, new Block(BlockTypes.Cannon));

            bool applied = ship.ApplyPowerup(variant: 1, baseTypeId: BlockTypes.Cannon,
                worldPoint: new float2(0.5f, 1.5f), seconds: 0.1f);
            Assert.IsTrue(applied);

            ship.Grid.TryGet(nearKey, out var near);
            ship.Grid.TryGet(farKey, out var far);
            Assert.AreEqual(1, BlockVariants.Get(near.Modifiers), "the nearer cannon must be the one transformed");
            Assert.AreEqual(0, BlockVariants.Get(far.Modifiers), "the farther cannon must be untouched");
            Assert.Greater(ship.VariantTimeLeft(nearKey), 0f);

            // Step past the powerup's lifetime; it must revert to variant 0.
            for (int i = 0; i < 10; i++) ship.Step(new ShipInput(0f, 0f, false), 1f / 60f);

            ship.Grid.TryGet(nearKey, out near);
            Assert.AreEqual(0, BlockVariants.Get(near.Modifiers), "an expired powerup must revert to the base variant");
            Assert.AreEqual(0f, ship.VariantTimeLeft(nearKey));
        }

        [Test]
        public void ApplyPowerup_NeverDisturbsFacingOrRampBits()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            int key = BlockKey.Pack(0, 1);
            byte facingAndRamp = (byte)(1 | 0b1100); // facing = +x, ramp level 3
            ship.Grid.TryAdd(key, new Block(BlockTypes.Cannon, facingAndRamp));

            ship.ApplyPowerup(variant: 1, baseTypeId: BlockTypes.Cannon,
                worldPoint: new float2(0.5f, 1.5f), seconds: 5f);

            ship.Grid.TryGet(key, out var block);
            Assert.AreEqual(facingAndRamp & Facing.Mask, block.Modifiers & Facing.Mask);
            Assert.AreEqual(facingAndRamp & ThrusterUpgrades.Mask, block.Modifiers & ThrusterUpgrades.Mask);
            Assert.AreEqual(1, BlockVariants.Get(block.Modifiers));
        }
    }
}
