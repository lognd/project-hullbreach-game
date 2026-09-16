using System;
using Unity.Mathematics;
using Hullbreach.Core;

namespace Hullbreach.Ship
{
    /// <summary>
    /// The ship simulation. PLAIN C# ON PURPOSE -- no UnityEngine anywhere in
    /// this assembly.
    ///
    /// That constraint buys three things: edit-mode tests that run in
    /// milliseconds without a scene, Burst-compilable hot paths, and a
    /// simulation the headless server (S47) can run without Unity's object
    /// model. The MonoBehaviour that drives this (ShipController) is a thin
    /// adapter holding lifecycle and Inspector wiring, and nothing else.
    /// </summary>
    public sealed class ShipBody
    {
        public readonly BlockGrid Grid = new BlockGrid();

        public float2 Position;
        public float Rotation;
        public float2 Velocity;
        public float AngularVelocity;

        /// <summary>Thruster block keys, rebuilt when topology is dirty.
        /// A dense typed list, because systems iterate "all thrusters" rather
        /// than dispatching polymorphically over all blocks.</summary>
        // TODO [A4]: Rebuild from the grid whenever TopologyDirty.
        public int[] ThrusterKeys = Array.Empty<int>();

        // TODO [A4]
        public int[] WeaponKeys = Array.Empty<int>();

        // TODO [A4]: Rebuild the typed index lists from the grid.
        public void RebuildDerivedViews()
            => throw new NotImplementedException();

        /// <summary>
        /// Advance one FIXED timestep. Never call this from Update: the physics
        /// step runs on a fixed timer, and applying force per rendered frame
        /// makes a 144 Hz machine fly differently from a 60 Hz one -- and both
        /// differently from the headless server.
        /// </summary>
        // TODO [A5]: For each thruster, apply force at its MOUNT POINT, not at
        //            the center of mass. Force applied at an offset point
        //            produces torque as a consequence of the physics, which is
        //            S39 criterion 1 falling out for free rather than being
        //            special-cased.
        //
        //            Criterion 2 (double the mass, halve the acceleration)
        //            comes free from a = F / Grid.Mass.Total.
        public void Step(in ShipInput input, float dt)
            => throw new NotImplementedException();

        // TODO [A5]: Accumulate a force applied at a ship-local point. Should
        //            update both the linear accumulator and the torque about
        //            the center of mass: tau = r x F, with r measured from the
        //            CoM, and in 2D  r x F = r.x * F.y - r.y * F.x.
        public void AddForceAtPoint(float2 shipLocalPoint, float2 force)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// One tick of player intent. A value type, so it is trivially
    /// serializable for the netcode and trivially constructible in tests.
    /// </summary>
    public readonly struct ShipInput
    {
        /// <summary>Main thrust held this tick.</summary>
        public readonly bool Thrusting;

        /// <summary>Steering axis, -1 .. +1.</summary>
        public readonly float Steer;

        /// <summary>Fire was PRESSED this tick (edge, not level).</summary>
        public readonly bool FirePressed;

        public ShipInput(bool thrusting, float steer, bool firePressed)
        {
            Thrusting = thrusting;
            Steer = steer;
            FirePressed = firePressed;
        }
    }
}
