using System.Collections.Generic;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Platform;
using NUnit.Framework;

namespace Hullbreach.Net.Tests
{
    // Pins the mid-match block economy: stock is spent, never invented; refusals carry a reason and do not
    // distrust; pre-match building is bounded by a budget; wreckage becomes salvage that is conserved.
    // frob:ticket 01M7EYG4KH9BT32Q4ERZVBQ3M4
    public class BlockEconomyTests
    {
        static SnapshotBlock Core() => new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0);

        static ShipSnapshot Ship(params SnapshotBlock[] extra)
        {
            var blocks = new List<SnapshotBlock> { Core() };
            blocks.AddRange(extra);
            return new ShipSnapshot(0, 0, blocks.ToArray(), 0, 0, 0, 0, 0, 0);
        }

        static ServerSimulation Server(bool live, float cooldown = 0.2f)
        {
            var server = new ServerSimulation(new ServerOutbox()) { TickRate = 50f, BuildCooldownSeconds = cooldown, MatchFeed = new MatchEndFeed() };
            server.Join(1, Ship());
            if (live) server.Join(2, Ship());
            Assert.AreEqual(live, server.Match != null);
            return server;
        }

        static void Pump(ServerSimulation server, int ticks, uint start = 0)
        {
            for (int i = 0; i < ticks; i++)
            {
                server.SetInput(1, new InputMessage(1, start + (uint)i, 0, 0, InputMessage.BuildModeBit));
                server.SetInput(2, new InputMessage(2, start + (uint)i, 0, 0, InputMessage.BuildModeBit));
                server.Tick();
            }
        }

        static void BuildMode(ServerSimulation server, uint tick = 1)
        {
            server.SetInput(1, new InputMessage(1, tick, 0, 0, InputMessage.BuildModeBit));
            server.SetInput(2, new InputMessage(2, tick, 0, 0, InputMessage.BuildModeBit));
        }

        [Test]
        public void LiveBuild_SpendsStock_ThenRefusesWithAReason_AndNoDistrust()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.Economy.cs::Hullbreach.Net.ServerSimulation.PlaceBlock kind="unit"
            ServerSimulation s = Server(true);
            BuildMode(s);
            Assert.AreEqual(3, s.StockOf(1)[BlockTypes.Hull]);
            int y = 1;
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(PlacementResult.Ok, s.PlaceBlock(1, 0, y++, BlockTypes.Hull, 0));
                Pump(s, 12, (uint)(2 + i * 12));
            }
            Assert.AreEqual(0, s.StockOf(1)[BlockTypes.Hull]);
            float trust = s.TrustOf(1);
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(PlacementResult.OutOfStock, s.PlaceBlock(1, 0, y, BlockTypes.Hull, 0));
            Assert.AreEqual(trust, s.TrustOf(1));
            Assert.AreEqual(0, s.StockOf(1)[BlockTypes.Hull], "refusals never go negative");
            Assert.AreEqual(4, s.Ships[1].Grid.Count);
        }

        [Test]
        public void StockIsPerPeer_AndTypeSpecific()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.Economy.cs::Hullbreach.Net.ServerSimulation.PlaceBlock kind="unit"
            ServerSimulation s = Server(true);
            BuildMode(s);
            Assert.AreEqual(PlacementResult.OutOfStock, s.PlaceBlock(1, 0, 1, BlockTypes.Cannon, 0), "no cannon in the starter kit");
            Assert.AreEqual(0, s.StockOf(2)[BlockTypes.Cannon]);
            Assert.IsTrue(s.GrantStock(1, BlockTypes.Cannon, 1));
            Assert.AreEqual(PlacementResult.Ok, s.PlaceBlock(1, 0, 1, BlockTypes.Cannon, 0));
            Assert.AreEqual(3, s.StockOf(2)[BlockTypes.Hull], "the other peer is untouched");
        }

        [Test]
        public void CoreCannotBeBuiltMidMatch()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/EconomyRules.cs::Hullbreach.Net.EconomyRules.CheckPlacement kind="unit"
            ServerSimulation s = Server(true);
            BuildMode(s);
            Assert.AreEqual(PlacementResult.CoreNotBuildable, s.PlaceBlock(1, 0, 1, BlockTypes.Core, 0));
        }

        [Test]
        public void FreshBlock_StartsDamaged_AndHardens()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.Economy.cs::Hullbreach.Net.ServerSimulation.PlaceBlock kind="unit"
            ServerSimulation s = Server(true);
            BuildMode(s);
            Assert.AreEqual(PlacementResult.Ok, s.PlaceBlock(1, 0, 1, BlockTypes.Hull, 0));
            s.Ships[1].Grid.TryGet(BlockKey.Pack(0, 1), out Block fresh);
            Assert.Greater(fresh.Damage, 0);
            Pump(s, 8 * 50 + 50, 2);
            s.Ships[1].Grid.TryGet(BlockKey.Pack(0, 1), out Block done);
            Assert.AreEqual(0, done.Damage, "construction finished");
        }

        [Test]
        public void Deconstruct_RefundsHalf_NeverMoreThanWasSpent()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.Economy.cs::Hullbreach.Net.ServerSimulation.Deconstruct kind="unit"
            ServerSimulation s = Server(true);
            BuildMode(s);
            int tick = 2;
            int built = 0, returned = 0;
            for (int loop = 0; loop < 12; loop++)
            {
                if (s.PlaceBlock(1, 0, 1, BlockTypes.Hull, 0) == PlacementResult.Ok) built++;
                Pump(s, 12, (uint)tick); tick += 12;
                if (s.Deconstruct(1, 0, 1) == PlacementResult.Ok) returned++;
                Pump(s, 12, (uint)tick); tick += 12;
            }
            int stock = s.StockOf(1)[BlockTypes.Hull];
            int onShip = s.Ships[1].Grid.Count - 1;
            int onField = 0;
            foreach (var p in s.Pickups) if (p.Kind == Hullbreach.World.PickupKind.Salvage) onField++;
            Assert.LessOrEqual(stock + onShip, 3, "a build/deconstruct loop never mints blocks");
            Assert.Greater(built, 0);
            Assert.GreaterOrEqual(built, returned);
            Assert.AreEqual(0, onField);
        }

        [Test]
        public void DestroyedBlock_MayLeaveSalvage_ThatTheSurvivorCanCollect()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.Economy.cs::Hullbreach.Net.ServerSimulation.DropSalvage kind="unit"
            int dropped = 0;
            for (int i = 0; i < 12; i++)
            {
                ServerSimulation s = Server(true);
                BuildMode(s);
                s.GrantStock(1, BlockTypes.Armor, 6);
                int tick = 2;
                for (int y = 1; y <= 6; y++)
                {
                    s.PlaceBlock(1, 0, y, BlockTypes.Armor, 0);
                    Pump(s, 12, (uint)tick); tick += 12;
                }
                for (int y = 6; y >= 1; y--) s.DebugDestroyBlock(1, 0, y);
                foreach (var p in s.Pickups) if (p.Kind == Hullbreach.World.PickupKind.Salvage) dropped++;
                if (dropped > 0) break;
            }
            Assert.Greater(dropped, 0, "70% salvage over many wrecks drops something");
        }

        [Test]
        public void PreMatch_BudgetAndSizeCap_RejectOversizedDesigns()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/ServerSimulation.Economy.cs::Hullbreach.Net.ServerSimulation.CheckDesignAgainstEconomy kind="unit"
            var server = new ServerSimulation(new ServerOutbox()) { MatchFeed = new MatchEndFeed() };
            var cannons = new List<SnapshotBlock> { Core() };
            for (int y = 1; y <= 14; y++) cannons.Add(new SnapshotBlock(0, (sbyte)y, BlockTypes.Cannon, 0, 0));
            var heavy = new ShipSnapshot(0, 0, cannons.ToArray(), 0, 0, 0, 0, 0, 0);
            Assert.AreEqual(JoinResult.OverBudget, server.Join(1, heavy), "14 cannons cost far more than 60 points");

            var tiny = new List<SnapshotBlock> { Core() };
            for (int y = 1; y <= 45; y++) tiny.Add(new SnapshotBlock(0, (sbyte)y, BlockTypes.Hull, 0, 0));
            var tooMany = new ShipSnapshot(0, 0, tiny.ToArray(), 0, 0, 0, 0, 0, 0);
            Assert.AreEqual(JoinResult.DesignTooLarge, server.Join(2, tooMany));
        }

        [Test]
        public void EconomyOff_KeepsLegacyFreeBuilding()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/EconomyConfig.cs::Hullbreach.Net.EconomyConfig.WithEnabled kind="unit"
            ServerSimulation s = Server(true);
            s.Economy = EconomyConfig.Default().WithEnabled(false);
            BuildMode(s);
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(PlacementResult.Ok, s.PlaceBlock(1, 0, 1 + i, BlockTypes.Hull, 0));
                Pump(s, 12, (uint)(2 + 12 * i));
            }
        }

        [Test]
        public void EconomyConfig_Defaults_AreValid_AndBadValuesRejected()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/EconomyConfig.cs::Hullbreach.Net.EconomyConfig.Validate kind="unit"
            Assert.IsNull(EconomyConfig.Default().Validate());
            var big = new int[BlockTypes.Count];
            big[BlockTypes.Hull] = 999;
            Assert.IsNotNull(EconomyConfig.Default().WithStartingStock(big).Validate());
            Assert.IsNotNull(EconomyConfig.Default().WithStartingStock(new int[2]).Validate());
        }

        [Test]
        public void Messages_RoundTrip_AndMalformedInputFailsCleanly()
        {
            // frob:tests Assets/Scripts/Hullbreach.Net/EconomyMessages.cs::Hullbreach.Net.InventorySnapshot.Read kind="unit"
            var buf = new byte[256];
            var w = new ByteWriter(buf);
            var inv = new InventorySnapshot(77u, new byte[] { 0, 3, 2, 1, 0, 0, 5 }, -1, 9, 40, 25);
            inv.Write(ref w);
            var r = new ByteReader(buf);
            InventorySnapshot back = InventorySnapshot.Read(ref r);
            Assert.IsFalse(r.Failed);
            Assert.AreEqual(inv.Tick, back.Tick);
            Assert.AreEqual(inv.Counts, back.Counts);
            Assert.AreEqual(inv.BudgetLeft, back.BudgetLeft);
            Assert.AreEqual(inv.CooldownTicks, back.CooldownTicks);

            var rng = new System.Random(5);
            for (int i = 0; i < 300; i++)
            {
                var junk = new byte[rng.Next(0, 40)];
                rng.NextBytes(junk);
                var jr = new ByteReader(junk);
                InventorySnapshot.Read(ref jr);
                var jr2 = new ByteReader(junk);
                PlacementResultMessage.Read(ref jr2);
                var jr3 = new ByteReader(junk);
                PickupSpawned.Read(ref jr3);
                var jr4 = new ByteReader(junk);
                PickupDespawned.Read(ref jr4);
            }
        }
    }
}
