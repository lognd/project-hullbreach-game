using NUnit.Framework;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;
using Hullbreach.World;

namespace Hullbreach.Ship.Tests
{
    // Regression guard: once steady-state collections have grown to their
    // final capacity, Step must not allocate at all. ShipBody alone.
    public class ShipBodyAllocationTests
    {
        // Builds a 50-block ship so RebuildDerivedViews has real key
        // arrays to build once (topology never changes after this).
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

            // A distant, weak field: body-force gravity runs every tick,
            // but never triggers the (unexercised) surface-contact path.
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
            // reusable collections grow to steady-state capacity.
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
