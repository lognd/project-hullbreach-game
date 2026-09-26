using Unity.Mathematics;
using Hullbreach.Core;

namespace Hullbreach.Ship.Behaviours
{
    // Everything an IBlockBehaviour needs for one block on one Step: a
    // plain mutable struct passed by ref, never boxed or stored.
    // frob:doc docs/reference/hullbreach-ship.md#blockcontext
    public struct BlockContext
    {
        // frob:doc docs/reference/hullbreach-ship.md#blockcontext
        public ShipBody Ship;

        // frob:doc docs/reference/hullbreach-ship.md#blockcontext
        public int Key;

        // frob:doc docs/reference/hullbreach-ship.md#blockcontext
        public Block Block;

        // frob:doc docs/reference/hullbreach-ship.md#blockcontext
        public float2 LocalCenter;

        // frob:doc docs/reference/hullbreach-ship.md#blockcontext
        public float2 WorldCenter;

        // Thrusters ignore this: they have a fixed direction of their own.
        // frob:doc docs/reference/hullbreach-ship.md#blockcontext
        public float2 Facing;

        // frob:doc docs/reference/hullbreach-ship.md#blockcontext
        public float Dt;

        // frob:doc docs/reference/hullbreach-ship.md#blockcontext
        public ShipInput Input;

        // Never null: ShipBody defaults it to NullWorldSink.Instance.
        // frob:doc docs/reference/hullbreach-ship.md#blockcontext
        public IWorldSink World;

        // Lands in both the force/torque accumulators and
        // AppliedForcesThisStep like every other applied force.
        // frob:doc docs/reference/hullbreach-ship.md#blockcontext
        public void AddForceLocal(float2 force) => Ship.AddForceAtPoint(LocalCenter, force);

        // Shared by every ramped channel; one block belongs to exactly
        // one behaviour so keys never collide.
        // frob:doc docs/reference/hullbreach-ship.md#blockcontext
        public float Throttle(float target) => Ship.RampThrottleFor(Key, Block.Modifiers, target, Dt);

        // frob:doc docs/reference/hullbreach-ship.md#blockcontext
        public float TickCooldown() => Ship.TickCooldownFor(Key, Dt);

        // frob:doc docs/reference/hullbreach-ship.md#blockcontext
        public void ResetCooldown(float seconds) => Ship.SetCooldownFor(Key, seconds);

        // ShipBody itself never instantiates a projectile object.
        // frob:doc docs/reference/hullbreach-ship.md#blockcontext
        public void Fire(in ShotRequest shot) => Ship.PendingShots.Add(shot);
    }
}
