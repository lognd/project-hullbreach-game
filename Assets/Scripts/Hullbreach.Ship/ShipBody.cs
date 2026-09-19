using System;
using System.Collections.Generic;
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

        /// <summary>Tunable force one thruster block contributes while
        /// Thrusting is held, in ship-local newtons-per-block.</summary>
        public float ThrustPerBlock = 10f;

        /// <summary>Thruster block keys, rebuilt when topology is dirty.
        /// A dense typed list, because systems iterate "all thrusters" rather
        /// than dispatching polymorphically over all blocks.</summary>
        public int[] ThrusterKeys = Array.Empty<int>();

        /// <summary>Cannon/weapon block keys, rebuilt when topology is dirty.
        /// Nothing consumes this yet -- combat is a later branch -- but the
        /// view is built here so that branch does not need to touch ShipBody.</summary>
        public int[] WeaponKeys = Array.Empty<int>();

        /// <summary>Set when FirePressed arrives in Step, for a later combat
        /// system to consume. This is deliberately the simplest possible
        /// hand-off: a flag, not an event, because nothing downstream exists
        /// yet to justify more machinery.</summary>
        public bool FireRequested { get; private set; }

        /// <summary>Linear acceleration computed by the last Step, in world
        /// space. Exposed (not just consumed internally) because the FE
        /// inertia-relief work on another branch needs the same a = F/M this
        /// integrator already computed.</summary>
        public float2 LastLinearAcceleration { get; private set; }

        /// <summary>Angular acceleration computed by the last Step.</summary>
        public float LastAngularAcceleration { get; private set; }

        float2 _forceAccum;
        float _torqueAccum;

        /// <summary>
        /// Rebuild ThrusterKeys and WeaponKeys from the grid. O(block count);
        /// called lazily from Step only when Grid.TopologyDirty, so placing or
        /// removing blocks is what pays this cost, not every physics tick.
        /// </summary>
        public void RebuildDerivedViews()
        {
            var thrusters = new List<int>();
            var weapons = new List<int>();
            foreach (var kv in Grid.All)
            {
                if (kv.Value.TypeId == BlockTypes.Thruster) thrusters.Add(kv.Key);
                else if (kv.Value.TypeId == BlockTypes.Cannon) weapons.Add(kv.Key);
            }
            ThrusterKeys = thrusters.ToArray();
            WeaponKeys = weapons.ToArray();
            Grid.ClearDirty();
        }

        /// <summary>
        /// Advance one FIXED timestep. Never call this from Update: the physics
        /// step runs on a fixed timer, and applying force per rendered frame
        /// makes a 144 Hz machine fly differently from a 60 Hz one -- and both
        /// differently from the headless server.
        ///
        /// Forces/torques are accumulated in SHIP-LOCAL space (thrusters and
        /// fins are fixed to the hull), then the net force is rotated into
        /// world space before integrating -- torque is a scalar about the
        /// out-of-plane axis and is unaffected by that rotation.
        /// </summary>
        public void Step(in ShipInput input, float dt)
        {
            if (Grid.TopologyDirty) RebuildDerivedViews();

            FireRequested = input.FirePressed;

            float mass = Grid.Mass.Total;
            if (mass <= 0f)
            {
                // No blocks (not even a core survives debris-splitting down to
                // nothing): there is nothing to push, so skip integration
                // entirely rather than divide by zero.
                LastLinearAcceleration = float2.zero;
                LastAngularAcceleration = 0f;
                return;
            }

            float2 com = Grid.Mass.CenterOfMass;

            if (input.Thrusting)
            {
                foreach (int key in ThrusterKeys)
                {
                    if (!Grid.TryGet(key, out var block)) continue;
                    float2 facing = BlockFacing.FromModifiers(block.Modifiers);
                    // Force pushes the ship in the thruster's facing
                    // direction; the exhaust goes the other way.
                    AddForceAtPoint(BlockGrid.CenterOf(key), facing * ThrustPerBlock);
                }
            }

            float leverArm = SteeringLeverArm(com);
            float currentThrust = input.Thrusting ? 1f : 0f;
            _torqueAccum += SteeringModel.Torque(input.Steer, currentThrust, leverArm,
                Grid.Mass.InertiaAboutCenterOfMass);

            float inertia = Grid.Mass.InertiaAboutCenterOfMass;

            float2 worldForce = RotateByRotation(_forceAccum);
            float2 a = worldForce / mass;
            float alpha = inertia > 0f ? _torqueAccum / inertia : 0f;

            LastLinearAcceleration = a;
            LastAngularAcceleration = alpha;

            // Semi-implicit (symplectic) Euler: update velocity first, then
            // use the NEW velocity to update position. More stable than
            // explicit Euler for the same dt, and it is what Box2D itself
            // does, so this stays consistent with how the rest of the
            // physics feels once handed to Rigidbody2D.
            Velocity += a * dt;
            AngularVelocity += alpha * dt;
            Position += Velocity * dt;
            Rotation += AngularVelocity * dt;

            _forceAccum = float2.zero;
            _torqueAccum = 0f;
        }

        /// <summary>
        /// Mean distance of thruster blocks from the center of mass, floored
        /// at 0.5 so a ship with no thrusters yet (or all of them stacked on
        /// the CoM) can still steer a little rather than not at all. There is
        /// no dedicated fin block type yet, so thruster placement stands in
        /// for it -- moving your thrusters out to the wingtips is currently
        /// the only way to make the ship turn better.
        /// </summary>
        float SteeringLeverArm(float2 com)
        {
            if (ThrusterKeys.Length == 0) return 0.5f;

            float sum = 0f;
            foreach (int key in ThrusterKeys)
            {
                sum += math.length(BlockGrid.CenterOf(key) - com);
            }
            return math.max(0.5f, sum / ThrusterKeys.Length);
        }

        float2 RotateByRotation(float2 v)
        {
            float s = math.sin(Rotation);
            float c = math.cos(Rotation);
            return new float2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>
        /// Accumulate a force applied at a ship-local point. Updates both the
        /// linear accumulator and the torque about the center of mass:
        /// tau = r x F, with r measured from the CoM, and in 2D
        /// r x F = r.x * F.y - r.y * F.x.
        /// </summary>
        public void AddForceAtPoint(float2 shipLocalPoint, float2 force)
        {
            _forceAccum += force;
            float2 r = shipLocalPoint - Grid.Mass.CenterOfMass;
            _torqueAccum += r.x * force.y - r.y * force.x;
        }
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
