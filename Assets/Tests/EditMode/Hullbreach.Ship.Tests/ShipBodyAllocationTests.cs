using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;
using Hullbreach.World;

namespace Hullbreach.Ship.Tests
{
    /// <summary>
    /// Regression guard for the BlockGrid/ShipBody per-tick allocation work:
    /// once RebuildDerivedViews has run once and every steady-state
    /// collection (ContactsThisStep, AppliedForcesThisStep, the boxed grid
    /// enumerators) has grown to its final capacity, Step must not allocate
    /// at all on a ShipBody exercising gravity, thrust and steering
    /// together. If this starts failing after a change in
    /// Hullbreach.Structure (StructuralSolver reading the same grid), that
    /// is out of this ticket's scope; ShipBody alone is what this asserts.
    /// </summary>
    public class ShipBodyAllocationTests
    {
        /// <summary>
        /// Builds a 50-block ship (core, a scattering of thrusters, retros
        /// and fins) so RebuildDerivedViews has real per-behaviour key
        /// arrays to build once, then never again (topology never changes
        /// after this).
        /// </summary>
        static ShipBody Build50BlockShip()
        {
            var ship = new ShipBody();
            ship.Grid.TryAdd(BlockKey.Pack(0, 0), new Block(BlockTypes.Core));

            int placed = 1;
            int x = 1;
            while (placed < 50)
            {
                byte typeId = (placed % 3 == 0) ? BlockTypes.Thruster
                            : (placed % 3 == 1) ? BlockTypes.Fin
                            : BlockTypes.RetroThruster;
                ship.Grid.TryAdd(BlockKey.Pack(x, 0), new Block(typeId));
                x++;
                placed++;
            }

            // A distant, weak field: tidal/body-force gravity runs every
            // tick, but the ship never gets close enough to trigger the
            // surface-contact path (a different code path this test does
            // not exercise).
            var field = new GravityField();
            field.Add(new GravityBody(new float2(0f, -100000f), mu: 10f, radius: 1f, surfaceRestitution: 0.5f));
            ship.Gravity = field;

            return ship;
        }

        [Test]
        public void Step_100Times_AllocatesNothingAfterSteadyState()
        {
            var ship = Build50BlockShip();
            var input = new ShipInput(thrustAxis: 1f, steer: 0.5f, firePressed: false);
            const float dt = 1f / 60f;

            // Step twice to pay for the one-time topology rebuild and let
            // every reusable collection (ContactsThisStep,
            // AppliedForcesThisStep, per-key throttle dictionaries) grow to
            // its steady-state capacity.
            ship.Step(input, dt);
            ship.Step(input, dt);

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 98; i++)
            {
                ship.Step(input, dt);
            }
            long after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(0, after - before,
                "ShipBody.Step must not allocate once past the steady state");
        }
    }
}
