using NUnit.Framework;

namespace Hullbreach.Hud.Tests
{
    public sealed class DamageNumberAggregatorTests
    {
        [Test]
        public void FirstHit_SpawnsANewNumber()
        {
            var a = new DamageNumberAggregator();
            var r = a.Add(0f, 0f, 20f, false, 0f);
            Assert.IsFalse(r.Merged);
            Assert.AreEqual(20f, r.Sum);
            Assert.AreEqual(0f, r.OffsetX);
            Assert.AreEqual(1, a.LiveCount);
        }

        [Test]
        public void NearbyHitsInTheWindow_MergeIntoOneSum()
        {
            var a = new DamageNumberAggregator();
            var first = a.Add(0f, 0f, 20f, false, 0f);
            var second = a.Add(0.5f, 0.2f, 15f, false, 0.1f);
            var third = a.Add(0.2f, 0f, 5f, true, 0.2f);
            Assert.IsTrue(second.Merged);
            Assert.AreEqual(first.Id, second.Id);
            Assert.AreEqual(35f, second.Sum);
            Assert.IsTrue(third.Merged);
            Assert.AreEqual(40f, third.Sum);
            Assert.IsTrue(third.Destroyed);
            Assert.AreEqual(1, a.LiveCount);
        }

        [Test]
        public void WindowIsMeasuredFromTheLatestHit()
        {
            var a = new DamageNumberAggregator();
            a.Add(0f, 0f, 10f, false, 0f);
            Assert.IsTrue(a.Add(0f, 0f, 10f, false, 0.2f).Merged);
            Assert.IsTrue(a.Add(0f, 0f, 10f, false, 0.4f).Merged, "0.2 s after the previous hit");
            Assert.IsFalse(a.Add(0f, 0f, 10f, false, 0.4f + DamageNumberAggregator.MergeWindow + 0.05f).Merged);
        }

        [Test]
        public void FarApartHits_DoNotMerge()
        {
            var a = new DamageNumberAggregator();
            a.Add(0f, 0f, 10f, false, 0f);
            Assert.IsFalse(a.Add(DamageNumberAggregator.MergeRadius + 0.5f, 0f, 10f, false, 0.05f).Merged);
        }

        [Test]
        public void ANumberStopsAbsorbingAfterItsMaxAge()
        {
            var a = new DamageNumberAggregator();
            a.Add(0f, 0f, 1f, false, 0f);
            float t = 0f;
            bool merged = true;
            while (t < 2f && merged) { t += 0.2f; merged = a.Add(0f, 0f, 1f, false, t).Merged; }
            Assert.IsFalse(merged);
            Assert.GreaterOrEqual(t, DamageNumberAggregator.MaxMergeAge);
        }

        [Test]
        public void SimultaneousSeparateNumbers_FanOutDeterministically()
        {
            var a = new DamageNumberAggregator();
            var steps = new float[5];
            for (int i = 0; i < 5; i++)
                steps[i] = a.Add(i * 5f, 0f, 10f, false, 0f).OffsetX / DamageNumberAggregator.SpreadStep; // far apart, same instant
            Assert.AreEqual(new[] { 0f, 1f, -1f, 2f, -2f }, steps);

            var b = new DamageNumberAggregator();
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(steps[i] * DamageNumberAggregator.SpreadStep, b.Add(i * 5f, 0f, 10f, false, 0f).OffsetX, 1e-6f, "same input, same output");
        }

        [Test]
        public void SpacedOutNumbers_AllStartCentred()
        {
            var a = new DamageNumberAggregator();
            Assert.AreEqual(0f, a.Add(0f, 0f, 10f, false, 0f).OffsetX);
            Assert.AreEqual(0f, a.Add(20f, 0f, 10f, false, 1.5f).OffsetX);
        }

        [Test]
        public void OldNumbersArePruned_AndBadDamageIsClamped()
        {
            var a = new DamageNumberAggregator();
            a.Add(0f, 0f, 10f, false, 0f);
            var r = a.Add(30f, 0f, float.NaN, false, 10f);
            Assert.AreEqual(0f, r.Sum);
            Assert.AreEqual(1, a.LiveCount);
            a.Clear();
            Assert.AreEqual(0, a.LiveCount);
        }
    }
}
