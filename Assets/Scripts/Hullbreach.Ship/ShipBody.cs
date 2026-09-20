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

        /// <summary>Tunable force one FULLY THROTTLED forward thruster block
        /// contributes, in ship-local newtons-per-block.</summary>
        public float ThrustPerBlock = 10f;

        /// <summary>Tunable force one fully throttled retro thruster block
        /// contributes. Defaults to half of ThrustPerBlock -- retros are two
        /// small side nozzles, not a main engine.</summary>
        public float RetroThrustPerBlock = 5f;

        /// <summary>Force magnitude a fin applies at full (|throttle| = 1)
        /// steer authority.</summary>
        public float FinForce = 6f;

        /// <summary>Seconds between shots for a single cannon block.</summary>
        public float CannonCooldown = 0.35f;

        /// <summary>Projectile parameters every cannon on this ship fires.</summary>
        public ProjectileSpec Projectile = ProjectileSpec.Default;

        /// <summary>Thruster block keys, rebuilt when topology is dirty.
        /// A dense typed list, because systems iterate "all thrusters" rather
        /// than dispatching polymorphically over all blocks.</summary>
        public int[] ThrusterKeys = Array.Empty<int>();

        /// <summary>Retro thruster block keys, rebuilt when topology is dirty.</summary>
        public int[] RetroKeys = Array.Empty<int>();

        /// <summary>Fin block keys, rebuilt when topology is dirty.</summary>
        public int[] FinKeys = Array.Empty<int>();

        /// <summary>Cannon/weapon block keys, rebuilt when topology is dirty.</summary>
        public int[] WeaponKeys = Array.Empty<int>();

        /// <summary>Set when FirePressed arrives in Step, for a later combat
        /// system to consume. This is deliberately the simplest possible
        /// hand-off: a flag, not an event, because nothing downstream exists
        /// yet to justify more machinery.</summary>
        public bool FireRequested { get; private set; }

        /// <summary>Shots fired this Step, appended to and left for the
        /// caller (ShipController) to drain and clear. ShipBody never spawns
        /// projectile objects itself -- it only records intent and applies
        /// its own recoil.</summary>
        public readonly List<ShotRequest> PendingShots = new List<ShotRequest>();

        /// <summary>Linear acceleration computed by the last Step, in world
        /// space. Exposed (not just consumed internally) because the FE
        /// inertia-relief work on another branch needs the same a = F/M this
        /// integrator already computed.</summary>
        public float2 LastLinearAcceleration { get; private set; }

        /// <summary>Angular acceleration computed by the last Step.</summary>
        public float LastAngularAcceleration { get; private set; }

        float2 _forceAccum;
        float _torqueAccum;

        /// <summary>Per-thruster/retro throttle 0..1, ramped toward the
        /// forward/reverse channel target at that block's own upgrade rate.
        /// Keyed by grid key since Thruster and RetroThruster keys never
        /// collide (one block per cell).</summary>
        readonly Dictionary<int, float> _throttleByKey = new Dictionary<int, float>();

        /// <summary>Per-fin steer throttle -1..1, ramped toward the steer
        /// channel target at that fin's own upgrade rate. Its SIGN (not the
        /// raw Steer input) decides which way the fin pushes, so torque
        /// fades out after the key is released instead of cutting instantly.</summary>
        readonly Dictionary<int, float> _finThrottleByKey = new Dictionary<int, float>();

        /// <summary>Seconds remaining before each cannon can fire again.</summary>
        readonly Dictionary<int, float> _cannonCooldownByKey = new Dictionary<int, float>();

        /// <summary>
        /// Rebuild ThrusterKeys/RetroKeys/FinKeys/WeaponKeys from the grid.
        /// O(block count); called lazily from Step only when
        /// Grid.TopologyDirty, so placing or removing blocks is what pays
        /// this cost, not every physics tick. Per-key ramp/cooldown state is
        /// pruned to the surviving keys but otherwise preserved, so placing
        /// an unrelated block does not reset an in-progress throttle ramp.
        /// </summary>
        public void RebuildDerivedViews()
        {
            var thrusters = new List<int>();
            var retros = new List<int>();
            var fins = new List<int>();
            var weapons = new List<int>();
            foreach (var kv in Grid.All)
            {
                switch (kv.Value.TypeId)
                {
                    case BlockTypes.Thruster: thrusters.Add(kv.Key); break;
                    case BlockTypes.RetroThruster: retros.Add(kv.Key); break;
                    case BlockTypes.Fin: fins.Add(kv.Key); break;
                    case BlockTypes.Cannon: weapons.Add(kv.Key); break;
                }
            }
            ThrusterKeys = thrusters.ToArray();
            RetroKeys = retros.ToArray();
            FinKeys = fins.ToArray();
            WeaponKeys = weapons.ToArray();

            PruneStale(_throttleByKey, ThrusterKeys, RetroKeys);
            PruneStale(_finThrottleByKey, FinKeys);
            PruneStale(_cannonCooldownByKey, WeaponKeys);

            Grid.ClearDirty();
        }

        static void PruneStale(Dictionary<int, float> dict, params int[][] survivingKeySets)
        {
            var stale = new List<int>();
            foreach (int key in dict.Keys)
            {
                bool survives = false;
                foreach (var set in survivingKeySets)
                {
                    if (Array.IndexOf(set, key) >= 0) { survives = true; break; }
                }
                if (!survives) stale.Add(key);
            }
            foreach (int key in stale) dict.Remove(key);
        }

        /// <summary>Current throttle 0..1 of the thruster/retro at `key`, for
        /// the renderer to size its flame effect.</summary>
        public float Throttle(int key) => _throttleByKey.TryGetValue(key, out var t) ? t : 0f;

        /// <summary>Current steer throttle -1..1 of the fin at `key`, for the
        /// renderer to animate the control surface.</summary>
        public float SteerThrottle(int key) => _finThrottleByKey.TryGetValue(key, out var t) ? t : 0f;

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
        ///
        /// Three ramped control channels drive everything: forward
        /// (target 1 while ThrustAxis &gt; 0), reverse (target 1 while
        /// ThrustAxis &lt; 0) and steer (target Steer, -1..1). Each thruster,
        /// retro and fin block ramps its OWN throttle toward its channel's
        /// target at a rate from its own upgrade bits, so releasing a key
        /// fades the effect out rather than cutting it instantly.
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

            float forwardTarget = input.ThrustAxis > 0f ? 1f : 0f;
            float reverseTarget = input.ThrustAxis < 0f ? 1f : 0f;
            float steerTarget = math.clamp(input.Steer, -1f, 1f);

            StepThrusters(ThrusterKeys, forwardTarget, new float2(0f, 1f), ThrustPerBlock, dt);
            StepThrusters(RetroKeys, reverseTarget, new float2(0f, -1f), RetroThrustPerBlock, dt);
            StepFins(steerTarget, com, dt);
            StepCannons(input.FirePressed, dt);

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
        /// Forward/retro thrusters have NO facing -- a forward thruster
        /// always pushes ship-local +y and a retro always pushes ship-local
        /// -y, regardless of Modifiers. `localDirection` carries that fixed
        /// direction; only the throttle ramps.
        /// </summary>
        void StepThrusters(int[] keys, float target, float2 localDirection, float forcePerBlock, float dt)
        {
            foreach (int key in keys)
            {
                if (!Grid.TryGet(key, out var block)) continue;
                float rate = ThrusterUpgrades.RampRate(block.Modifiers);
                float current = _throttleByKey.TryGetValue(key, out var t) ? t : 0f;
                float updated = RampToward(current, target, rate, dt);
                _throttleByKey[key] = updated;
                if (updated == 0f) continue;
                AddForceAtPoint(BlockGrid.CenterOf(key), localDirection * updated * forcePerBlock);
            }
        }

        /// <summary>
        /// Each fin ramps its own throttle toward `steerTarget`, then pushes
        /// perpendicular to its facing with a sign chosen so the resulting
        /// torque about the center of mass matches the sign of that fin's
        /// CURRENT ramped throttle (not the raw target) -- this is what lets
        /// torque fade out smoothly after the steer key is released instead
        /// of cutting the instant Steer returns to zero.
        /// </summary>
        void StepFins(float steerTarget, float2 com, float dt)
        {
            foreach (int key in FinKeys)
            {
                if (!Grid.TryGet(key, out var block)) continue;
                float rate = ThrusterUpgrades.RampRate(block.Modifiers);
                float current = _finThrottleByKey.TryGetValue(key, out var t) ? t : 0f;
                float updated = RampToward(current, steerTarget, rate, dt);
                _finThrottleByKey[key] = updated;
                if (updated == 0f) continue;

                float2 perp = Hullbreach.Core.Facing.Perpendicular(block.Modifiers);
                float2 center = BlockGrid.CenterOf(key);
                float2 r = center - com;
                float crossRPerp = r.x * perp.y - r.y * perp.x;

                // Force = perp * mag gives torque = mag * crossRPerp. Flip
                // perp's sign when that disagrees with the throttle's sign so
                // every fin helps turn the requested way.
                float2 direction = (crossRPerp >= 0f) == (updated >= 0f) ? perp : -perp;
                float2 force = direction * math.abs(updated) * FinForce;
                AddForceAtPoint(center, force);
            }
        }

        /// <summary>
        /// Ticks every cannon's cooldown down by dt, and on FirePressed fires
        /// every cannon that is ready: records a ShotRequest for the caller
        /// to spawn and applies the recoil impulse to this ship immediately.
        /// </summary>
        void StepCannons(bool firePressed, float dt)
        {
            foreach (int key in WeaponKeys)
            {
                float remaining = _cannonCooldownByKey.TryGetValue(key, out var rem) ? rem : 0f;
                remaining = math.max(0f, remaining - dt);

                if (firePressed && remaining <= 0f)
                {
                    if (!Grid.TryGet(key, out var block)) { _cannonCooldownByKey[key] = remaining; continue; }

                    float2 facing = BlockFacing.FromModifiers(block.Modifiers);
                    float2 muzzleLocal = BlockGrid.CenterOf(key) + facing * 0.6f;
                    float2 worldDirection = RotateByRotation(facing);
                    float2 worldOrigin = LocalToWorld(muzzleLocal);

                    PendingShots.Add(new ShotRequest(key, worldOrigin, worldDirection, Projectile));

                    float2 mountWorld = LocalToWorld(BlockGrid.CenterOf(key));
                    ApplyImpulseAtWorldPoint(mountWorld, -worldDirection * Projectile.Impulse);

                    remaining = CannonCooldown;
                }

                _cannonCooldownByKey[key] = remaining;
            }
        }

        /// <summary>Ramp `current` toward `target` by at most `ratePerSecond
        /// * dt`, landing exactly on target rather than overshooting.</summary>
        static float RampToward(float current, float target, float ratePerSecond, float dt)
        {
            float maxDelta = ratePerSecond * dt;
            float diff = target - current;
            if (math.abs(diff) <= maxDelta) return target;
            return current + math.sign(diff) * maxDelta;
        }

        float2 RotateByRotation(float2 v)
        {
            float s = math.sin(Rotation);
            float c = math.cos(Rotation);
            return new float2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>Ship-local point to world space: rotate by Rotation, then
        /// translate by Position.</summary>
        public float2 LocalToWorld(float2 localPoint) => RotateByRotation(localPoint) + Position;

        /// <summary>World point to ship-local space: exact inverse of
        /// LocalToWorld.</summary>
        public float2 WorldToLocal(float2 worldPoint)
        {
            float2 d = worldPoint - Position;
            float s = math.sin(-Rotation);
            float c = math.cos(-Rotation);
            return new float2(d.x * c - d.y * s, d.x * s + d.y * c);
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

        /// <summary>
        /// Apply an INSTANTANEOUS impulse at a world-space point: dv = J/M,
        /// dw = cross(r, J)/I with r measured from the world-space center of
        /// mass. Used for cannon recoil, which should feel like a kick right
        /// now rather than a force integrated over the next dt.
        /// </summary>
        public void ApplyImpulseAtWorldPoint(float2 worldPoint, float2 impulse)
        {
            float mass = Grid.Mass.Total;
            if (mass <= 0f) return;

            Velocity += impulse / mass;

            float inertia = Grid.Mass.InertiaAboutCenterOfMass;
            if (inertia > 0f)
            {
                float2 comWorld = LocalToWorld(Grid.Mass.CenterOfMass);
                float2 r = worldPoint - comWorld;
                AngularVelocity += (r.x * impulse.y - r.y * impulse.x) / inertia;
            }
        }

        /// <summary>
        /// Apply `damage` (saturating) to whatever block occupies the grid
        /// cell under a world-space point, e.g. a projectile hit. Returns
        /// whether a block was actually there; `key` is the grid key checked
        /// (valid even on a miss, -1 only if the point falls outside the
        /// packable grid range).
        /// </summary>
        public bool ApplyDamageAtWorldPoint(float2 worldPoint, byte damage, out int key)
        {
            float2 local = WorldToLocal(worldPoint);
            int x = (int)math.floor(local.x);
            int y = (int)math.floor(local.y);

            if (!BlockKey.InRange(x, y)) { key = -1; return false; }
            key = BlockKey.Pack(x, y);

            if (!Grid.TryGet(key, out var block)) return false;

            int newDamage = math.min(255, block.Damage + damage);
            return Grid.TrySet(key, block.WithDamage((byte)newDamage));
        }
    }

    /// <summary>
    /// One tick of player intent. A value type, so it is trivially
    /// serializable for the netcode and trivially constructible in tests.
    /// </summary>
    public readonly struct ShipInput
    {
        /// <summary>Main thrust axis, -1..1: positive fires forward
        /// thrusters, negative fires retro thrusters, zero fires neither.</summary>
        public readonly float ThrustAxis;

        /// <summary>Steering axis, -1 .. +1.</summary>
        public readonly float Steer;

        /// <summary>Fire was PRESSED this tick (edge, not level).</summary>
        public readonly bool FirePressed;

        public ShipInput(float thrustAxis, float steer, bool firePressed)
        {
            ThrustAxis = thrustAxis;
            Steer = steer;
            FirePressed = firePressed;
        }
    }
}
