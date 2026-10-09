using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Game;
using Hullbreach.Ship;

namespace Hullbreach.Demo.Tests
{
    // A ship has one trigger collider per block, so a round straddling two
    // blocks must still deliver its damage exactly once.
    public sealed class ProjectileTests : DemoSceneFixture
    {
        static int TotalDamage(BlockGrid grid)
        {
            int sum = 0;
            foreach (var kvp in grid.All) sum += kvp.Value.Damage;
            return sum;
        }

        // frob:tests Assets/Scripts/Hullbreach.Game/Projectile.cs::Projectile
        [UnityTest]
        public IEnumerator RoundStraddlingTwoBlocks_DamagesOnce()
        {
            yield return LoadDemoScene();
            var grid = Player.Ship.Grid;

            // Midpoint of the edge between the first two adjacent blocks.
            var neighbors = new int[4];
            float2 edgeLocal = float2.zero;
            bool found = false;
            foreach (var kvp in grid.All)
            {
                BlockKey.Neighbors(kvp.Key, neighbors);
                foreach (int n in neighbors)
                {
                    if (n < 0 || !grid.Contains(n)) continue;
                    edgeLocal = (BlockGrid.CenterOf(kvp.Key) + BlockGrid.CenterOf(n)) * 0.5f;
                    found = true;
                    break;
                }
                if (found) break;
            }
            Assert.IsTrue(found, "the player ship needs two adjacent blocks");

            int before = TotalDamage(grid);
            var spec = new ProjectileSpec(speed: 0f, impulse: 0f, damage: 20, lifetimeSeconds: 2f, radius: 0.2f);
            var go = new GameObject("TestProjectile", typeof(Rigidbody2D), typeof(CircleCollider2D));
            var world = Player.Ship.LocalToWorld(edgeLocal);
            go.AddComponent<Projectile>().Configure(new Vector2(world.x, world.y), Vector2.up, Vector2.zero, spec, null);

            yield return FixedSteps(0.2f);

            Assert.AreEqual(before + 20, TotalDamage(grid), "one round must deal its damage once, not once per collider");
        }
    }
}
