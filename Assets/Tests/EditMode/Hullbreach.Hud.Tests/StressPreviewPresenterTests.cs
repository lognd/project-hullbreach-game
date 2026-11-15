using NUnit.Framework;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Ship;
using Hullbreach.Structure;

namespace Hullbreach.Hud.Tests
{
    public class StressPreviewPresenterTests
    {
        // The PR #167 playtest ship: wide, tip thrusters, 22 blocks. Peaks near 2.7 (WillBreak).
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

        static BlockGrid CoreAndThruster()
        {
            var g = new BlockGrid();
            g.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));
            g.TryAdd(BlockKey.Pack(0, -1), new Block(BlockTypes.Thruster));
            return g;
        }

        [Test]
        public void Thresholds_MatchTheDamageModel()
        {
            Assert.AreEqual(DamageModel.CreakRatio, StressTintModel.AmberAt);
            Assert.AreEqual(DamageModel.FailRatio, StressTintModel.RedAt);
        }

        [Test]
        public void Tint_IsGreenBelowPointEight_AmberToOne_RedAtOneAndAbove()
        {
            HudColor green = StressTintModel.Tint(0.79f, false);
            HudColor amber = StressTintModel.Tint(0.8f, false);
            HudColor amber2 = StressTintModel.Tint(0.99f, false);
            HudColor red = StressTintModel.Tint(1.0f, false);
            HudColor red2 = StressTintModel.Tint(2.7f, false);
            Assert.Greater(green.G, green.R);
            Assert.AreEqual(amber.R, amber2.R);
            Assert.Greater(amber.R, amber.B);
            Assert.Greater(amber.G, 0.5f);
            Assert.Greater(red.R, red.G * 3f);
            Assert.AreEqual(red.R, red2.R);
            Assert.AreNotEqual(green.G, red.G);
        }

        [Test]
        public void Tint_ColorblindRamp_DiffersFromStandard()
        {
            foreach (float risk in new[] { 0.2f, 0.9f, 1.5f })
                Assert.AreNotEqual(StressTintModel.Tint(risk, false), StressTintModel.Tint(risk, true));
        }

        [Test]
        public void StatusLine_NamesEachVerdict()
        {
            Assert.AreEqual("Structure: Safe", StressTintModel.StatusLine(StressVerdict.Safe));
            Assert.AreEqual("Structure: Marginal", StressTintModel.StatusLine(StressVerdict.Marginal));
            Assert.AreEqual("Structure: Will snap under full thrust", StressTintModel.StatusLine(StressVerdict.WillBreak));
            Assert.AreEqual(StressTintModel.Tint(1f, false), StressTintModel.StatusColor(StressVerdict.WillBreak, false));
        }

        [Test]
        public void Observe_DebouncesEdits_IntoOneEvaluation()
        {
            var grid = CoreAndThruster();
            var p = new StressPreviewPresenter();
            p.Observe(grid, 0f);
            Assert.AreEqual(0, p.Evaluations, "not yet: the debounce has not elapsed");
            grid.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull));
            p.Observe(grid, 0.1f);
            Assert.AreEqual(0, p.Evaluations, "second edit restarts the wait");
            p.Observe(grid, 0.2f);
            Assert.AreEqual(0, p.Evaluations);
            p.Observe(grid, 0.26f);
            Assert.AreEqual(1, p.Evaluations);
            Assert.IsNotNull(p.Result);
            p.Observe(grid, 5f);
            Assert.AreEqual(1, p.Evaluations, "an unchanged grid is never re-solved");
        }

        [Test]
        public void Observe_Remove_Reevaluates()
        {
            var grid = CoreAndThruster();
            grid.TryAdd(BlockKey.Pack(1, 0), new Block(BlockTypes.Hull));
            var p = new StressPreviewPresenter();
            p.Observe(grid, 0f);
            p.Observe(grid, 0.2f);
            Assert.AreEqual(1, p.Evaluations);
            grid.TryRemove(BlockKey.Pack(1, 0));
            p.Observe(grid, 1f);
            p.Observe(grid, 1.2f);
            Assert.AreEqual(2, p.Evaluations);
        }

        [Test]
        public void SafeShip_ShowsSafe_NoPulse_AndTintsEveryBlockGreen()
        {
            var grid = CoreAndThruster();
            var p = new StressPreviewPresenter();
            p.Observe(grid, 0f);
            p.Observe(grid, 0.2f);
            Assert.AreEqual("Structure: Safe", p.Status);
            Assert.AreEqual(0f, p.PulseFor(p.Result.WeakestKey, 0.125f));
            HudColor? c = p.TintFor(BlockKey.Pack(0, -1), false);
            Assert.IsTrue(c.HasValue);
            Assert.Greater(c.Value.G, c.Value.R);
            p.FocusWeakest(1f);
            Assert.Greater(p.PulseFor(p.Result.WeakestKey, 1.125f), 0f, "Fix it pulses even a Safe ship's weakest block");
        }

        [Test]
        public void WideWing_IsRed_Pulses_AndReadyWarns()
        {
            var grid = WideWing();
            var p = new StressPreviewPresenter();
            p.Observe(grid, 0f);
            p.Observe(grid, 0.2f);
            Assert.AreEqual(StressVerdict.WillBreak, p.Result.Verdict);
            Assert.AreEqual("Structure: Will snap under full thrust", p.Status);
            int weakest = p.Result.WeakestKey;
            HudColor? c = p.TintFor(weakest, false);
            Assert.IsTrue(c.HasValue);
            Assert.Greater(c.Value.R, c.Value.G * 3f);
            Assert.Greater(p.PulseFor(weakest, 0.125f), 0f);
            Assert.AreEqual(0f, p.PulseFor(BlockKey.Pack(0, 0) == weakest ? BlockKey.Pack(0, 1) : BlockKey.Pack(0, 0), 0.125f), "only the weakest block pulses");
            string warning = StressPreviewPresenter.ReadyWarning(grid);
            Assert.IsNotNull(warning);
            StringAssert.Contains("snap", warning);
        }

        [Test]
        public void ReadyWarning_IsNullForEveryStarterTemplate_AndEmptyGrid()
        {
            foreach (var t in ShipTemplates.All)
            {
                Assert.IsTrue(ShipDesignCodec.TryBuildGrid(t.Blocks, out var grid, out string why), why);
                Assert.IsNull(StressPreviewPresenter.ReadyWarning(grid), t.Id);
                var p = new StressPreviewPresenter();
                p.Observe(grid, 0f);
                p.Observe(grid, 0.2f);
                Assert.AreEqual(StressVerdict.Safe, p.Result.Verdict, t.Id + " peak " + p.Result.PeakRisk);
            }
            Assert.IsNull(StressPreviewPresenter.ReadyWarning(null));
            Assert.IsNull(StressPreviewPresenter.ReadyWarning(new BlockGrid()));
        }

        [Test]
        public void Reset_ClearsTheResult()
        {
            var p = new StressPreviewPresenter();
            p.Observe(CoreAndThruster(), 0f);
            p.Observe(CoreAndThruster(), 0.2f);
            p.Reset();
            Assert.IsNull(p.Result);
            Assert.AreEqual(string.Empty, p.Status);
            Assert.IsNull(p.TintFor(0, false));
        }
    }
}
