using Unity.Mathematics;
using Hullbreach.Core;

namespace Hullbreach.Ship.Behaviours
{
    /// <summary>
    /// Everything an IBlockBehaviour needs to act for one block on one Step,
    /// bundled so ShipBody.Step can fill in one instance per block without
    /// allocating: a plain mutable struct passed by ref, never boxed, never
    /// stored past the call that filled it in.
    /// </summary>
    public struct BlockContext
    {
        /// <summary>The ship this block belongs to.</summary>
        public ShipBody Ship;

        /// <summary>Packed grid key of the block.</summary>
        public int Key;

        /// <summary>The block itself (TypeId/Modifiers/Damage snapshot).</summary>
        public Block Block;

        /// <summary>Ship-local center of the block's cell.</summary>
        public float2 LocalCenter;

        /// <summary>World-space center of the block's cell this Step.</summary>
        public float2 WorldCenter;

        /// <summary>Ship-local facing direction decoded from Block.Modifiers,
        /// for directional blocks (Cannon/Fin). Thrusters ignore this: they
        /// have a fixed direction of their own.</summary>
        public float2 Facing;

        /// <summary>Fixed timestep this Step is advancing by.</summary>
        public float Dt;

        /// <summary>This tick's player intent.</summary>
        public ShipInput Input;

        /// <summary>The game-side sink for anything a behaviour needs the
        /// world for (spawning projectiles, temporary gravity, targeting).
        /// Never null: ShipBody defaults it to NullWorldSink.Instance.</summary>
        public IWorldSink World;

        /// <summary>Accumulates `force` (ship-local) at this block's ship-local
        /// center via Ship.AddForceAtPoint, so it lands in both the force/torque
        /// accumulators and AppliedForcesThisStep like every other applied force.</summary>
        public void AddForceLocal(float2 force) => Ship.AddForceAtPoint(LocalCenter, force);

        /// <summary>Ramps this block's own throttle toward `target` at a rate
        /// decided by its ramp-upgrade bits (ThrusterUpgrades.RampRate),
        /// stores the updated value keyed by Key, and returns it. Shared by
        /// every ramped channel (forward/retro thrust, fin steer) since one
        /// block belongs to exactly one behaviour and keys never collide.</summary>
        public float Throttle(float target) => Ship.RampThrottleFor(Key, Block.Modifiers, target, Dt);

        /// <summary>Ticks this block's cooldown down by Dt (floored at zero)
        /// and returns the seconds remaining afterward.</summary>
        public float TickCooldown() => Ship.TickCooldownFor(Key, Dt);

        /// <summary>Sets this block's cooldown to `seconds`, e.g. right after
        /// firing.</summary>
        public void ResetCooldown(float seconds) => Ship.SetCooldownFor(Key, seconds);

        /// <summary>Records a shot for the caller (ShipController) to spawn;
        /// ShipBody itself never instantiates a projectile object.</summary>
        public void Fire(in ShotRequest shot) => Ship.PendingShots.Add(shot);
    }
}
