using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hullbreach.Game;

namespace Hullbreach.Demo.Tests
{
    // Ships created after the spawner's Start must still be wired for shots.
    public sealed class ProjectileSpawnerTests : DemoSceneFixture
    {
        // frob:tests Assets/Scripts/Hullbreach.Game/ProjectileSpawner.cs::ProjectileSpawner
        [UnityTest]
        public IEnumerator LateShip_IsWiredAndItsShotsSpawnProjectiles()
        {
            yield return LoadDemoScene();
            var spawner = Object.FindAnyObjectByType<ProjectileSpawner>();
            Assert.IsNotNull(spawner);

            var go = new GameObject("LateShip", typeof(Rigidbody2D));
            var late = go.AddComponent<ShipController>();
            yield return null;
            WorldSink.Instance.Refresh();

            int before = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length;
            late.RequestFire();
            yield return FixedSteps(0.2f);

            Assert.Greater(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length, before,
                "a ship created after Start must have its shots spawned as projectiles");
            Object.Destroy(go);
        }
    }
}
