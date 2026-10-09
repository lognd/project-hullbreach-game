using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Mathematics;
using Hullbreach.Game;
using Hullbreach.World;

namespace Hullbreach.Demo.Tests
{
    // GravityWorld owns the static field: it must tick temporary wells and
    // must not let a stale instance blank a live one.
    public sealed class GravityWorldTests
    {
        GameObject _a, _b;

        [TearDown]
        public void TearDown()
        {
            if (_a != null) Object.Destroy(_a);
            if (_b != null) Object.Destroy(_b);
        }

        // frob:tests Assets/Scripts/Hullbreach.Game/GravityWorld.cs::GravityWorld
        [UnityTest]
        public IEnumerator TemporaryWell_ExpiresInTheClient()
        {
            _a = new GameObject("GravityWorldA");
            _a.AddComponent<GravityWorld>();
            yield return null;

            var field = GravityWorld.Field;
            Assert.IsNotNull(field);
            int baseline = field.Count;
            field.AddTemporary(new GravityBody(float2.zero, 10f, 1f, 0.2f), 0.1f);
            Assert.AreEqual(baseline + 1, field.Count);

            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(baseline, field.Count, "the temporary well must expire once GravityWorld ticks its field");
        }

        // frob:tests Assets/Scripts/Hullbreach.Game/GravityWorld.cs::GravityWorld
        [UnityTest]
        public IEnumerator DestroyingAStaleInstance_DoesNotBlankTheLiveField()
        {
            _a = new GameObject("GravityWorldA");
            _a.AddComponent<GravityWorld>();
            yield return null;
            var first = GravityWorld.Field;

            _b = new GameObject("GravityWorldB");
            _b.AddComponent<GravityWorld>();
            yield return null;
            var live = GravityWorld.Field;
            Assert.AreNotSame(first, live);

            Object.Destroy(_a);
            yield return null;
            Assert.AreSame(live, GravityWorld.Field, "destroying the older instance must not clear the newer field");

            Object.Destroy(_b);
            yield return null;
            Assert.IsNull(GravityWorld.Field);
        }
    }
}
