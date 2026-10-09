using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hullbreach.Core;
using Hullbreach.Game;

namespace Hullbreach.Demo.Tests
{
    // Build mode freezes the ship, so the structural solve must freeze too.
    public sealed class ShipStructureBuildModeTests : DemoSceneFixture
    {
        static int TotalDamage(BlockGrid grid)
        {
            int sum = 0;
            foreach (var kvp in grid.All) sum += kvp.Value.Damage;
            return sum;
        }

        // frob:tests Assets/Scripts/Hullbreach.Game/ShipStructure.cs::ShipStructure
        [UnityTest]
        public IEnumerator HighLoadShip_TakesNoDamageWhileInBuildMode()
        {
            yield return LoadDemoScene();
            Demo.PlayerStructure.Solver.LoadScale = 5f; // overstress the ship under thrust
            Demo.SetState(DemoState.Fly);
            Input.Thrust = 1f;
            yield return FixedSteps(0.5f);

            Demo.SetState(DemoState.Build);
            yield return new WaitForFixedUpdate();
            int count = Player.Ship.Grid.Count;
            int damage = TotalDamage(Player.Ship.Grid);

            yield return FixedSteps(3f);

            Assert.AreEqual(count, Player.Ship.Grid.Count, "Build mode must not detach blocks");
            Assert.AreEqual(damage, TotalDamage(Player.Ship.Grid), "Build mode must not accumulate damage");
        }
    }
}
