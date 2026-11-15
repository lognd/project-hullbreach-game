using System.Collections.Generic;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Net.Tests
{
    // The server side of one-click starter ships and the remembered last design.
    // frob:doc docs/reference/hullbreach-net.md#serversimulation
    public class ApplyDesignTests
    {
        static ServerSimulation Lobby(int peers)
        {
            var s = new ServerSimulation(new ServerOutbox()) { TickRate = 50f, MatchFeed = new MatchEndFeed() };
            for (int p = 1; p <= peers; p++)
                s.Join(p, new ShipSnapshot(0, 0, new[] { new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0) }, 0, 0, 0, 0, 0, 0));
            return s;
        }

        // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.Economy.cs::Hullbreach.Net.ServerSimulation.ApplyDesign kind="unit"
        [Test]
        public void EveryTemplate_ReplacesTheShip_AndStaysInsideTheBudget()
        {
            ServerSimulation s = Lobby(1);
            foreach (ShipTemplate t in ShipTemplates.All)
            {
                Assert.AreEqual(PlacementResult.Ok, s.ApplyDesign(1, t.Blocks), t.Id);
                Assert.AreEqual(t.Blocks.Count, s.Ships[1].Grid.Count, t.Id + " leaves stale blocks behind");
                Assert.GreaterOrEqual(s.BudgetLeftOf(1), 0, t.Id);
                Assert.AreEqual(ShipDesignCodec.Encode(t.Blocks).Split(';').Length, s.Ships[1].Grid.Count);
                foreach (DesignBlock b in t.Blocks)
                {
                    Assert.IsTrue(s.Ships[1].Grid.TryGet(BlockKey.Pack(b.X, b.Y), out Block got), t.Id);
                    Assert.AreEqual(b.TypeId, got.TypeId);
                }
            }
        }

        // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.Economy.cs::Hullbreach.Net.ServerSimulation.ApplyDesign kind="unit"
        [Test]
        public void IllegalOrOverBudgetDesigns_AreRefused_AndChangeNothing()
        {
            ServerSimulation s = Lobby(1);
            Assert.AreEqual(PlacementResult.Ok, s.ApplyDesign(1, ShipTemplates.Default.Blocks));
            string before = ShipDesignCodec.Encode(s.Ships[1].Grid);

            var armorSlab = new List<DesignBlock> { new DesignBlock(0, 0, BlockTypes.Core, 0) };
            for (int x = -3; x <= 3; x++) for (int y = -3; y <= 3; y++) if (x != 0 || y != 0) armorSlab.Add(new DesignBlock(x, y, BlockTypes.Armor, 0));
            Assert.AreEqual(PlacementResult.BadRequest, s.ApplyDesign(1, armorSlab), "over budget");
            Assert.AreEqual(PlacementResult.BadRequest, s.ApplyDesign(1, new List<DesignBlock> { new DesignBlock(0, 0, BlockTypes.Core, 0), new DesignBlock(5, 5, BlockTypes.Hull, 0) }), "floating block");
            Assert.AreEqual(PlacementResult.BadRequest, s.ApplyDesign(1, null));
            Assert.AreEqual(PlacementResult.NoShip, s.ApplyDesign(9, ShipTemplates.Default.Blocks));
            Assert.AreEqual(before, ShipDesignCodec.Encode(s.Ships[1].Grid));
        }

        // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.Economy.cs::Hullbreach.Net.ServerSimulation.ApplyDesign kind="unit"
        [Test]
        public void NoSwapOnceTheMatchIsRunning()
        {
            ServerSimulation s = Lobby(2);
            Assert.IsNotNull(s.Match);
            string before = ShipDesignCodec.Encode(s.Ships[1].Grid);
            Assert.AreEqual(PlacementResult.NotBuilding, s.ApplyDesign(1, ShipTemplates.ById("tank").Blocks));
            Assert.AreEqual(before, ShipDesignCodec.Encode(s.Ships[1].Grid));
        }
    }
}
