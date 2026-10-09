using NUnit.Framework;
using Hullbreach.Core;
using Hullbreach.Builder;

namespace Hullbreach.Builder.Tests
{
    public class BuildBudgetTests
    {
        // Small round numbers so the arithmetic in each test is checkable by eye.
        static readonly BuildTuning Tuning = new BuildTuning(10f, 12f, 1f, 0.5f);

        static BuilderSession MidMatchSession(out BuildBudget budget)
        {
            budget = new BuildBudget(Tuning);
            var session = new BuilderSession(new BlockGrid(), budget);
            session.Select(BlockTypes.Core);
            Assert.IsTrue(session.Click(BlockKey.Pack(0, 0)), "core seeds the grid");
            // Core costs 0 but still starts the cooldown; clear it so tests start ready.
            session.Tick(10f);
            return session;
        }

        [Test]
        public void StartsWithStartingCredits_AndChargesPaletteCost()
        {
            var budget = new BuildBudget(Tuning);
            Assert.AreEqual(10f, budget.Credits);
            Assert.IsTrue(budget.TryCharge(BlockTypes.Cannon, out var denial));
            Assert.AreEqual(BuildDenial.None, denial);
            Assert.AreEqual(10f - BlockPalette.CostOf(BlockTypes.Cannon), budget.Credits);
        }

        [Test]
        public void Cooldown_BlocksSecondChargeUntilTicked()
        {
            var budget = new BuildBudget(Tuning);
            budget.TryCharge(BlockTypes.Hull, out _);
            Assert.IsFalse(budget.TryCharge(BlockTypes.Hull, out var denial));
            Assert.AreEqual(BuildDenial.Cooldown, denial);

            budget.Tick(0.4f);
            Assert.AreEqual(BuildDenial.Cooldown, budget.Check(BlockTypes.Hull));
            budget.Tick(0.1f);
            Assert.AreEqual(BuildDenial.None, budget.Check(BlockTypes.Hull));
        }

        [Test]
        public void Exhaustion_ThenRefill()
        {
            var budget = new BuildBudget(new BuildTuning(5f, 12f, 1f, 0f));
            Assert.IsTrue(budget.TryCharge(BlockTypes.Cannon, out _));
            Assert.IsFalse(budget.TryCharge(BlockTypes.Armor, out var denial));
            Assert.AreEqual(BuildDenial.InsufficientCredits, denial);
            Assert.AreEqual(0f, budget.Credits, "a refused charge spends nothing");

            budget.Tick(3f);
            Assert.IsTrue(budget.TryCharge(BlockTypes.Armor, out _));
        }

        [Test]
        public void Refill_IsCappedAtMaxCredits()
        {
            var budget = new BuildBudget(Tuning);
            budget.Tick(1000f);
            Assert.AreEqual(12f, budget.Credits);
        }

        [Test]
        public void NegativeOrZeroTick_ChangesNothing()
        {
            var budget = new BuildBudget(Tuning);
            budget.TryCharge(BlockTypes.Hull, out _);
            float credits = budget.Credits;
            float cooldown = budget.CooldownRemaining;
            budget.Tick(-5f);
            budget.Tick(0f);
            Assert.AreEqual(credits, budget.Credits);
            Assert.AreEqual(cooldown, budget.CooldownRemaining);
        }

        [Test]
        public void Unlimited_NeverChargesOrDenies()
        {
            var budget = BuildBudget.Unlimited();
            for (int i = 0; i < 100; i++) Assert.IsTrue(budget.TryCharge(BlockTypes.Cannon, out _));
            Assert.AreEqual(0f, budget.CooldownRemaining);
        }

        [Test]
        public void SameTicksGiveSameState_ForServerAndClient()
        {
            var a = new BuildBudget(Tuning);
            var b = new BuildBudget(Tuning);
            foreach (var budget in new[] { a, b })
            {
                budget.TryCharge(BlockTypes.Armor, out _);
                budget.Tick(0.25f);
                budget.Tick(0.25f);
                budget.TryCharge(BlockTypes.Hull, out _);
            }
            Assert.AreEqual(a.Credits, b.Credits);
            Assert.AreEqual(a.CooldownRemaining, b.CooldownRemaining);
        }

        [Test]
        public void ShippedTuning_AllowsAnOpeningBuildButNotTheWholePalette()
        {
            var t = BuildTuning.MidMatch;
            Assert.Greater(t.StartingCredits, BlockPalette.CostOf(BlockTypes.Cannon));
            Assert.LessOrEqual(t.StartingCredits, t.MaxCredits);
            Assert.Greater(t.RefillPerSecond, 0f);
            Assert.Greater(t.CooldownSeconds, 0f);
        }

        [Test]
        public void Session_ChargesOnPlacement_AndDeniesDuringCooldown()
        {
            var session = MidMatchSession(out var budget);
            session.Select(BlockTypes.Hull);

            Assert.IsTrue(session.Click(BlockKey.Pack(1, 0)));
            Assert.AreEqual(11f, budget.Credits, "refilled to the 12 cap, minus a hull");

            Assert.IsFalse(session.Click(BlockKey.Pack(2, 0)));
            Assert.AreEqual(BuildDenial.Cooldown, session.LastDenial);
            Assert.AreEqual(2, session.BlockCount, "denied placement must not touch the grid");

            session.Tick(0.5f);
            Assert.IsTrue(session.Click(BlockKey.Pack(2, 0)));
            Assert.AreEqual(BuildDenial.None, session.LastDenial);
        }

        [Test]
        public void Session_DeniesWhenBroke_AndInvalidCellSpendsNothing()
        {
            var session = MidMatchSession(out var budget);
            session.Select(BlockTypes.Cannon);
            session.Tick(10f);
            // Not adjacent to anything: rules refuse before the budget is consulted.
            Assert.IsFalse(session.Click(BlockKey.Pack(9, 9)));
            Assert.AreEqual(BuildDenial.None, session.LastDenial);
            Assert.AreEqual(12f, budget.Credits);

            // Two cannons (5 each) fit in 12; the third does not.
            Assert.IsTrue(PlaceCannon(session, 1, 0));
            session.Tick(0.5f);
            Assert.IsTrue(PlaceCannon(session, -1, 0));
            session.Tick(0.5f);
            Assert.IsFalse(PlaceCannon(session, 0, 1));
            Assert.AreEqual(BuildDenial.InsufficientCredits, session.LastDenial);
        }

        static bool PlaceCannon(BuilderSession session, int x, int y)
        {
            session.Select(BlockTypes.Cannon);
            if (!session.Click(BlockKey.Pack(x, y))) return false;
            // Second click four cells straight up: faces the cannon along +y.
            return session.Click(BlockKey.Pack(x, y + 4));
        }

        [Test]
        public void Session_TwoClickBlock_IsChargedOnCommitNotOnFirstClick()
        {
            var session = MidMatchSession(out var budget);
            session.Select(BlockTypes.Fin);
            Assert.IsTrue(session.Click(BlockKey.Pack(1, 0)));
            Assert.AreEqual(12f, budget.Credits, "orienting is free");
            Assert.AreEqual(0f, budget.CooldownRemaining);
        }

        [Test]
        public void Session_DefaultConstructors_AreUnlimited()
        {
            var session = new BuilderSession();
            Assert.IsTrue(session.Budget.IsUnlimited);
        }
    }
}
