using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Game;

namespace Hullbreach.Demo.Tests
{
    // A ship has one trigger collider per block, so a pickup straddling two
    // blocks must apply once and queue exactly one respawn.
    public sealed class PowerupTests : DemoSceneFixture
    {
        // frob:tests Assets/Scripts/Hullbreach.Game/Powerup.cs::Powerup
        [UnityTest]
        public IEnumerator PickupStraddlingTwoBlocks_RespawnsOnce()
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

            var spawnerGo = new GameObject("TestPowerupSpawner");
            var spawner = spawnerGo.AddComponent<PowerupSpawner>();
            typeof(PowerupSpawner).GetField("respawnSeconds", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(spawner, 0.2f);

            var world = Player.Ship.LocalToWorld(edgeLocal);
            var preset = new PowerupPreset(new Vector2(world.x, world.y), 0, BlockTypes.Core, 5f, Color.white, "Test");
            var go = new GameObject("TestPowerup", typeof(CircleCollider2D));
            go.transform.SetParent(spawnerGo.transform, false);
            go.transform.position = preset.position;
            go.AddComponent<Powerup>().Configure(preset);

            yield return FixedSteps(0.1f);
            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(1, spawnerGo.GetComponentsInChildren<Powerup>().Length,
                "one pickup must queue exactly one respawn");
            Object.Destroy(spawnerGo);
        }
    }
}
