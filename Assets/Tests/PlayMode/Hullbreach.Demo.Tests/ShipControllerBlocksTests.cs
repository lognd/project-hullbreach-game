using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hullbreach.Core;
using Hullbreach.Game;

namespace Hullbreach.Demo.Tests
{
    // ReplaceBlocks reports what it could not apply instead of dropping blocks silently.
    public sealed class ShipControllerBlocksTests : DemoSceneFixture
    {
        // frob:tests Assets/Scripts/Hullbreach.Game/ShipController.cs::ShipController.ReplaceBlocks
        [UnityTest]
        public IEnumerator ReplaceBlocks_ReportsRejectedBlocks()
        {
            yield return LoadDemoScene();
            int? coreKey = Player.Ship.Grid.CoreKey;
            Assert.IsTrue(coreKey.HasValue);
            BlockKey.Unpack(coreKey.Value, out int cx, out int cy);

            // An out-of-range block is dropped, warned about, and reflected in the result.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("outside the grid"));
            var withBadBlock = new List<AuthoredBlock>
            {
                new AuthoredBlock(cx, cy, BlockTypes.Core),
                new AuthoredBlock(cx, cy + 1, BlockTypes.Hull),
                new AuthoredBlock(500, 0, BlockTypes.Hull),
            };
            Assert.IsFalse(Player.ReplaceBlocks(withBadBlock));
            Assert.AreEqual(2, Player.Ship.Grid.Count);
        }
    }
}
