using System;
using System.Diagnostics;
using NUnit.Framework;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Ship;

namespace Hullbreach.Ship.Tests
{
    public class ShipStressPreviewTests
    {
        // The playtest ship: wide, tip thrusters, 22 blocks (see docs/reference/hullbreach-ship.md#shipstresspreview).
        static BlockGrid WideWing()
        {
            var g = new BlockGrid();
            void P(int x, int y, byte t) => g.TryAdd(BlockKey.Pack(x, y), new Block(t));
            P(0, 0, BlockTypes.Core);
            P(0, 1, BlockTypes.Hull); P(0, -1, BlockTypes.Hull); P(0, 2, BlockTypes.Cannon);
            P(1, 1, BlockTypes.Fin); P(-1, 1, BlockTypes.Fin);
            for (int x = 1; x <= 3; x++) { P(x, 0, BlockTypes.Armor); P(-x, 0, BlockTypes.Armor); }
            for (int x = 4; x <= 6; x++) { P(x, 0, BlockTypes.Hull); P(-x, 0, BlockTypes.Hull); }
            foreach (int x in new[] { 5, 6 }) { P(x, -1, BlockTypes.Thruster); P(-x, -1, BlockTypes.Thruster); }
            return g;
        }

        // Same wing, thrusters moved in from the tips: less lever arm.
        static BlockGrid ShortLever()
        {
            var g = new BlockGrid();
            foreach (var kv in WideWing().All)
                if (kv.Value.TypeId != BlockTypes.Thruster) g.TryAdd(kv.Key, kv.Value);
            foreach (int x in new[] { 2, 3 })
            {
                g.TryAdd(BlockKey.Pack(x, -1), new Block(BlockTypes.Thruster));
                g.TryAdd(BlockKey.Pack(-x, -1), new Block(BlockTypes.Thruster));
            }
            return g;
        }

        [Test]
        public void WideWingWithTipThrusters_IsWillBreak_AndNamesTheWeakBlock()
        {
            var r = new ShipStressPreview().Evaluate(WideWing());
            BlockKey.Unpack(r.WeakestKey, out int wx, out int wy);
            TestContext.Progress.WriteLine($"wide wing preview: {r.Verdict} peak {r.PeakRisk:F2} at ({wx},{wy}) overloaded {r.OverloadedCount} secondsToFail {r.SecondsToFail:F2}");
            Assert.AreEqual(StressVerdict.WillBreak, r.Verdict);
            Assert.Greater(r.PeakRisk, 1.5f);
            Assert.Greater(r.OverloadedCount, 1);
            Assert.IsTrue(r.Risk.ContainsKey(r.WeakestKey));
            // WeakestKey is the lowest key among blocks within 0.5% of the peak
            // (mirror-image blocks tie within solver noise), so allow that band.
            Assert.AreEqual(r.PeakRisk, r.Risk[r.WeakestKey], 0.005f * r.PeakRisk);
            // It must fail slowly enough to see, not instantly.
            Assert.Greater(r.SecondsToFail, 0.5f);
            Assert.Less(r.SecondsToFail, 10f);
            StringAssert.Contains("snap", r.Warning);
        }

        [Test]
        public void ThrustersNearTheRoot_LowerTheRisk_AndAreNotWillBreak()
        {
            var p = new ShipStressPreview();
            var bad = p.Evaluate(WideWing());
            var good = p.Evaluate(ShortLever());
            TestContext.Progress.WriteLine($"short lever preview: {good.Verdict} peak {good.PeakRisk:F2}");
            Assert.Less(good.PeakRisk, bad.PeakRisk * 0.5f);
            Assert.AreNotEqual(StressVerdict.WillBreak, good.Verdict, "peak " + good.PeakRisk);
        }

        [Test]
        public void EveryStarterTemplate_IsNotWillBreak()
        {
            var preview = new ShipStressPreview { IncludeBuckling = true };
            foreach (var t in ShipTemplates.All)
            {
                Assert.IsTrue(ShipDesignCodec.TryBuildGrid(t.Blocks, out var grid, out string why), why);
                var r = preview.Evaluate(grid);
                Assert.AreNotEqual(StressVerdict.WillBreak, r.Verdict, t.Id + " peak " + r.PeakRisk);
                TestContext.Progress.WriteLine($"template {t.Id}: {r.Verdict} peak {r.PeakRisk:F2}");
            }
        }

        [Test]
        public void CoreWithOneThruster_IsSafe_WithNoWarning()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(0, -1), new Block(BlockTypes.Thruster));
            var r = new ShipStressPreview().Evaluate(g);
            Assert.AreEqual(StressVerdict.Safe, r.Verdict, "peak " + r.PeakRisk);
            Assert.AreEqual(float.PositiveInfinity, r.SecondsToFail);
            Assert.AreEqual(string.Empty, r.Warning);
        }

        [Test]
        public void EmptyAndNullGrids_AreSafeWithNoWeakestBlock()
        {
            var p = new ShipStressPreview();
            Assert.AreEqual(StressVerdict.Safe, p.Evaluate(new BlockGrid()).Verdict);
            var r = p.Evaluate(null);
            Assert.AreEqual(StressVerdict.Safe, r.Verdict);
            Assert.AreEqual(-1, r.WeakestKey);
        }

        [Test]
        public void Evaluate_DoesNotMutateTheGrid_AndIsRepeatable()
        {
            var g = WideWing();
            int count = g.Count;
            var p = new ShipStressPreview();
            var a = p.Evaluate(g);
            var b = p.Evaluate(g);
            Assert.AreEqual(count, g.Count);
            foreach (var kv in g.All) Assert.AreEqual(0, kv.Value.Damage);
            Assert.AreEqual(a.PeakRisk, b.PeakRisk, 1e-3f * a.PeakRisk);
            Assert.AreEqual(a.WeakestKey, b.WeakestKey);
        }

        [Test]
        public void MirrorImageBlocks_GetTheSameRisk()
        {
            var r = new ShipStressPreview().Evaluate(WideWing());
            foreach (var kv in r.Risk)
            {
                BlockKey.Unpack(kv.Key, out int x, out int y);
                if (x == 0) continue;
                float mirror = r.Risk[BlockKey.Pack(-x, y)];
                Assert.AreEqual(kv.Value, mirror, 0.05f * Math.Max(1f, kv.Value), "(" + x + "," + y + ")");
            }
        }

        static double BestOfMs(Action run, int n)
        {
            double best = double.MaxValue;
            var sw = new Stopwatch();
            for (int i = 0; i < n; i++)
            {
                sw.Restart();
                run();
                best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
            }
            return best;
        }

        [Test]
        public void Cost_IsBuildTimeCheap()
        {
            // About 50 blocks: the wide wing plus hull slabs.
            var g = WideWing();
            for (int x = -3; x <= 3; x++)
                for (int y = -3; y <= -2; y++)
                    g.TryAdd(BlockKey.Pack(x, y), new Block(BlockTypes.Hull));
            for (int x = -6; x <= 6 && g.Count < 50; x++) g.TryAdd(BlockKey.Pack(x, 3), new Block(BlockTypes.Hull));
            var fast = new ShipStressPreview();
            var full = new ShipStressPreview { IncludeBuckling = true };
            fast.Evaluate(g); full.Evaluate(g); // JIT warm-up
            // Best-of-N, not the mean: a build-time cost is the cost with no
            // scheduler preemption in it. A mean charges one descheduled
            // slice (CI under CPU contention) to the code under test.
            double fastMs = BestOfMs(() => fast.Evaluate(g), 7);
            double fullMs = BestOfMs(() => full.Evaluate(g), 7);
            TestContext.Progress.WriteLine($"ShipStressPreview {g.Count} blocks: fast {fastMs:F1} ms, with buckling {fullMs:F1} ms");
            Assert.Less(fastMs, 250.0);
            Assert.Less(fullMs, 1000.0);
        }
    }
}
