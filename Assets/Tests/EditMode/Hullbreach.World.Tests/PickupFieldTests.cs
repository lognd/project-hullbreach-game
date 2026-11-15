using System.Collections.Generic;
using Hullbreach.Core;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hullbreach.World.Tests
{
    // Pins the pickup field: crates are a pure function of the seed, salvage is bounded, and a pickup is only
    // grabbable slowly, closely and without a planet in the way.
    // frob:ticket 01M7EYG4KH9BT32Q4ERZVBQ3M4
    public class PickupFieldTests
    {
        const float Rate = 50f;

        static GravityField Planets()
        {
            var g = new GravityField();
            g.Add(new GravityBody(new float2(0f, -60f), 900f, 18f, 0.2f));
            return g;
        }

        static PickupField Make(uint seed, PickupConfig? config = null)
            => new PickupField(config ?? PickupConfig.Default(), seed, Rate, 0, float2.zero, 100f, Planets(), null, 0);

        static List<Pickup> Run(PickupField f, int ticks)
        {
            var all = new List<Pickup>();
            var events = new List<PickupEvent>();
            for (int t = 0; t <= ticks; t++)
            {
                events.Clear();
                f.Tick(t, events);
                foreach (PickupEvent e in events) if (e.Spawned) all.Add(e.Pickup);
            }
            return all;
        }

        [Test]
        public void Crates_ArePureInSeed()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/PickupField.cs::Hullbreach.World.PickupField.CrateWave kind="unit"
            List<Pickup> a = Run(Make(77u), 4000), b = Run(Make(77u), 4000), c = Run(Make(78u), 4000);
            Assert.Greater(a.Count, 5);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Id, b[i].Id);
                Assert.AreEqual(a[i].Type, b[i].Type);
                Assert.AreEqual(a[i].Origin.x, b[i].Origin.x);
            }
            bool differs = a.Count != c.Count;
            for (int i = 0; !differs && i < a.Count; i++) differs = a[i].Origin.x != c[i].Origin.x;
            Assert.IsTrue(differs, "another seed gives another layout");
        }

        [Test]
        public void CrateWave_DoesNotDependOnHistory()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/PickupField.cs::Hullbreach.World.PickupField.CrateWave kind="unit"
            var one = new List<Pickup>();
            var two = new List<Pickup>();
            Make(5u).CrateWave(4, one);
            PickupField busy = Make(5u);
            Run(busy, 3000);
            busy.CrateWave(4, two);
            Assert.AreEqual(one.Count, two.Count);
            for (int i = 0; i < one.Count; i++) Assert.AreEqual(one[i].Origin.x, two[i].Origin.x);
        }

        [Test]
        public void CratesHoldNoCore_AndAtLeastOneBlock()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/PickupField.cs::Hullbreach.World.PickupField.CrateWave kind="unit"
            for (uint seed = 1; seed < 40; seed++)
            {
                PickupField f = Make(seed);
                for (int index = 0; index < 9; index++)
                {
                    var wave = new List<Pickup>();
                    f.CrateWave(index, wave);
                    foreach (Pickup p in wave)
                    {
                        Assert.AreNotEqual(BlockTypes.Core, p.Type);
                        Assert.GreaterOrEqual(p.Count, 1);
                        Assert.GreaterOrEqual(p.Id, PickupField.CrateIdBase);
                    }
                }
            }
        }

        [Test]
        public void Salvage_IsCapped_OldestCulledFirst()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/PickupField.cs::Hullbreach.World.PickupField.AddSalvage kind="unit"
            var cfg = new PickupConfig(2f, 4f, 0f, 12f, 75f, 1f, 40f, 3, 0f);
            PickupField f = Make(1u, cfg);
            var events = new List<PickupEvent>();
            for (int i = 0; i < 5; i++) f.AddSalvage(0, BlockTypes.Hull, new float2(i, 0f), float2.zero, events);
            Assert.AreEqual(3, f.Active.Count);
            Assert.AreEqual(3u, f.Active[0].Id, "ids 1 and 2 were culled");
        }

        static bool Has(PickupField f, uint id)
        {
            foreach (Pickup q in f.Active) if (q.Id == id) return true;
            return false;
        }

        [Test]
        public void Pickup_ExpiresAfterItsLifetime()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/PickupField.cs::Hullbreach.World.PickupField.Tick kind="unit"
            PickupField f = Make(1u);
            var events = new List<PickupEvent>();
            Pickup p = f.AddSalvage(10, BlockTypes.Armor, float2.zero, float2.zero, events);
            f.Tick(p.ExpireTick - 1, events);
            Assert.IsTrue(Has(f, p.Id));
            f.Tick(p.ExpireTick, events);
            Assert.IsFalse(Has(f, p.Id));
        }

        [Test]
        public void Reach_NeedsProximity_LowRelativeSpeed_AndArming()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/PickupField.cs::Hullbreach.World.PickupField.Reach kind="unit"
            PickupField f = Make(1u);
            Pickup p = f.AddSalvage(0, BlockTypes.Hull, new float2(50f, 50f), new float2(1f, 0f), null);
            var near = new List<float2> { new float2(52f, 51.5f) };
            var far = new List<float2> { new float2(52f, 55f) };
            float2 v = new float2(1f, 0f);
            Assert.IsTrue(float.IsPositiveInfinity(f.Reach(p, 10, near, v)), "not armed yet");
            Assert.IsFalse(float.IsPositiveInfinity(f.Reach(p, 100, near, v)));
            Assert.IsTrue(float.IsPositiveInfinity(f.Reach(p, 100, far, v)), "outside the magnet");
            Assert.IsTrue(float.IsPositiveInfinity(f.Reach(p, 100, near, new float2(9f, 0f))), "too fast");
        }

        [Test]
        public void Reach_IsBlockedByAPlanet()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/PickupField.cs::Hullbreach.World.PickupField.Reach kind="unit"
            var planets = new GravityField();
            planets.Add(new GravityBody(float2.zero, 900f, 1.2f, 0.2f));
            var cfg = new PickupConfig(3f, 4f, 0f, 12f, 75f, 1f, 40f, 8, 0f);
            var f = new PickupField(cfg, 1u, Rate, 0, float2.zero, 100f, planets, null, 0);
            Pickup p = f.AddSalvage(0, BlockTypes.Hull, new float2(1.9f, 0f), float2.zero, null);
            var behind = new List<float2> { new float2(-1.9f, 0f) };
            Assert.IsTrue(float.IsPositiveInfinity(f.Reach(p, 5, behind, float2.zero)));
            Assert.IsTrue(f.SegmentHitsPlanet(new float2(1.9f, 0f), new float2(-1.9f, 0f)));
        }

        [Test]
        public void Remove_PaysOutOnce()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/PickupField.cs::Hullbreach.World.PickupField.Remove kind="unit"
            PickupField f = Make(1u);
            Pickup p = f.AddSalvage(0, BlockTypes.Hull, float2.zero, float2.zero, null);
            Assert.IsTrue(f.Remove(p.Id, PickupRemoval.Collected, 1, null));
            Assert.IsFalse(f.Remove(p.Id, PickupRemoval.Collected, 2, null));
        }

        [Test]
        public void Config_Validate_RejectsNonsense()
        {
            // frob:tests Assets/Scripts/Hullbreach.World/PickupField.cs::Hullbreach.World.PickupConfig.Validate kind="unit"
            Assert.IsNull(PickupConfig.Default().Validate());
            Assert.IsNotNull(new PickupConfig(0f, 4f, 20f, 12f, 75f, 1f, 40f, 96, 1.5f).Validate());
            Assert.IsNotNull(new PickupConfig(2f, 4f, 20f, 12f, 75f, 1f, 40f, 0, 1.5f).Validate());
        }
    }
}
