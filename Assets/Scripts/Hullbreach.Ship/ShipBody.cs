using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.World;

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

        /// <summary>The gravity field this ship falls in, or null for none
        /// (e.g. RocketScene, which never wires one up). Set by the caller
        /// (ShipController.Awake) rather than owned here, so the same
        /// ShipBody can be dropped into a field-less test without a stub.</summary>
        public GravityField Gravity;

        /// <summary>Relative contact speed (m/s) above which a planet impact
        /// starts dealing damage; below this, a landing is "soft" and does
        /// no damage at all.</summary>
        public float ContactDamageSpeed = 3f;

        /// <summary>Damage points per m/s of contact speed above
        /// ContactDamageSpeed.</summary>
        public float ContactDamagePerSpeed = 12f;

        /// <summary>Fraction of tangential contact velocity removed per
        /// second while a block is touching a planet's surface, approximating
        /// ground friction without a full friction-cone solve.</summary>
        public float Friction = 2f;

        /// <summary>Clearance (world units) added to a planet's Radius when
        /// testing block contact, so a block's own half-extent does not sink
        /// visibly into the surface before contact registers.</summary>
        public float ContactClearance = 0.5f;

        /// <summary>Every planet contact resolved this Step: which block
        /// (grid key), the surface normal at that block, and the impact
        /// speed along that normal. Cleared and repopulated every Step, for
        /// the renderer/audio to react to.</summary>
        public readonly List<(int key, float2 normal, float speed)> ContactsThisStep = new List<(int, float2, float)>();

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

        /// <summary>Every continuous force applied via AddForceAtPoint this
        /// Step (thrusters, retros, fins), in SHIP-LOCAL coordinates. Cleared
        /// at the start of every Step. A later structural system (ShipStructure)
        /// feeds these straight into StructuralSolver.Tick without ShipBody
        /// needing to know StructuralSolver exists.</summary>
        public readonly List<(float2 point, float2 force)> AppliedForcesThisStep = new List<(float2, float2)>();

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

        /// <summary>Reusable scratch buffer for sorting contacting block
        /// keys into deterministic order in ResolvePlanetContacts, so the
        /// per-tick contact pass never allocates a fresh list.</summary>
        readonly List<int> _contactKeyScratch = new List<int>();

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

            AppliedForcesThisStep.Clear();
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
            ApplyGravityForces();

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

            ResolvePlanetContacts(dt);
        }

        /// <summary>
        /// Adds gravity as a per-block BODY FORCE, before integration: each
        /// block's own weight m_i * g(worldCenter_i) is pushed through
        /// AddForceAtPoint at that block's ship-local center, so it is both
        /// torque-correct (a lopsided ship spins under a tidal gradient) and
        /// recorded in AppliedForcesThisStep for the structural solver.
        /// No-op (and allocation-free either way) when Gravity is unset.
        /// </summary>
        void ApplyGravityForces()
        {
            if (Gravity == null) return;

            foreach (var kv in Grid.All)
            {
                float2 localCenter = BlockGrid.CenterOf(kv.Key);
                float2 worldCenter = LocalToWorld(localCenter);
                float2 accel = Gravity.AccelerationAt(worldCenter);
                float blockMass = BlockTypes.Get(kv.Value.TypeId).Mass;
                float2 worldForce = accel * blockMass;
                float2 localForce = WorldVectorToLocal(worldForce);
                AddForceAtPoint(localCenter, localForce);
            }
        }

        /// <summary>
        /// After integration: tests every block center against Gravity for
        /// surface contact, pushes the ship out of the deepest single
        /// penetration once, then resolves each contacting block's normal
        /// velocity with a restitution impulse (ApplyImpulseAtWorldPoint),
        /// bleeds tangential velocity by Friction (a simple ground-friction
        /// approximation), and applies contact damage proportional to the
        /// impact speed above ContactDamageSpeed. Contacts are processed in
        /// sorted key order so the result is deterministic regardless of the
        /// grid's internal dictionary iteration order.
        /// </summary>
        void ResolvePlanetContacts(float dt)
        {
            ContactsThisStep.Clear();
            if (Gravity == null || Grid.Mass.Total <= 0f) return;

            _contactKeyScratch.Clear();
            foreach (var kv in Grid.All) _contactKeyScratch.Add(kv.Key);
            _contactKeyScratch.Sort();

            // Pass 1: find the single deepest penetration across all
            // contacting blocks and push the whole ship out along that
            // normal once, so multiple simultaneously-contacting blocks
            // (e.g. a flat hull landing) do not get pushed out repeatedly.
            float deepestPenetration = 0f;
            float2 deepestNormal = float2.zero;
            bool anyContact = false;

            for (int i = 0; i < _contactKeyScratch.Count; i++)
            {
                int key = _contactKeyScratch[i];
                float2 worldCenter = LocalToWorld(BlockGrid.CenterOf(key));
                if (!Gravity.TryContact(worldCenter, ContactClearance, out _, out var normal, out var penetration)) continue;
                anyContact = true;
                if (penetration > deepestPenetration)
                {
                    deepestPenetration = penetration;
                    deepestNormal = normal;
                }
            }

            if (anyContact && deepestPenetration > 0f)
            {
                Position += deepestNormal * deepestPenetration;
            }

            if (!anyContact) return;

            float mass = Grid.Mass.Total;
            float inertia = Grid.Mass.InertiaAboutCenterOfMass;

            for (int i = 0; i < _contactKeyScratch.Count; i++)
            {
                int key = _contactKeyScratch[i];
                float2 worldCenter = LocalToWorld(BlockGrid.CenterOf(key));
                if (!Gravity.TryContact(worldCenter, ContactClearance, out int bodyIndex, out var normal, out _)) continue;

                float2 r = worldCenter - LocalToWorld(Grid.Mass.CenterOfMass);
                float2 blockVelocity = Velocity + AngularVelocity * new float2(-r.y, r.x);
                float vn = math.dot(blockVelocity, normal);

                float restitution = Gravity.TryGetPermanent(bodyIndex, out var body) ? body.SurfaceRestitution : 0f;

                if (vn < 0f)
                {
                    float crossRN = r.x * normal.y - r.y * normal.x;
                    float denom = inertia > 0f
                        ? (1f / mass) + (crossRN * crossRN) / inertia
                        : (1f / mass);
                    float impulseMag = -(1f + restitution) * vn / denom;
                    float2 impulse = normal * impulseMag;
                    ApplyImpulseAtWorldPoint(worldCenter, impulse);
                }

                // Ground friction: bleed the tangential component of this
                // block's velocity, recomputed after the restitution impulse
                // above so friction acts on the post-bounce state.
                float2 tangent = new float2(-normal.y, normal.x);
                float2 postR = worldCenter - LocalToWorld(Grid.Mass.CenterOfMass);
                float2 postVelocity = Velocity + AngularVelocity * new float2(-postR.y, postR.x);
                float vt = math.dot(postVelocity, tangent);
                float frictionImpulseMag = -vt * math.clamp(Friction * dt, 0f, 1f) * mass;
                if (frictionImpulseMag != 0f)
                {
                    ApplyImpulseAtWorldPoint(worldCenter, tangent * frictionImpulseMag);
                }

                ContactsThisStep.Add((key, normal, math.abs(vn)));

                if (math.abs(vn) > ContactDamageSpeed && Grid.TryGet(key, out var block))
                {
                    float damageAmount = math.clamp((math.abs(vn) - ContactDamageSpeed) * ContactDamagePerSpeed, 0f, 255f);
                    int newDamage = math.min(255, block.Damage + (int)damageAmount);
                    Grid.TrySet(key, block.WithDamage((byte)newDamage));
                }
            }
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

        /// <summary>Rotates a world-space free VECTOR (force, direction --
        /// no translation) into ship-local space; the vector counterpart of
        /// WorldToLocal, used to fold a world-space gravity force into the
        /// ship-local force accumulator that AddForceAtPoint expects.</summary>
        float2 WorldVectorToLocal(float2 worldVector)
        {
            float s = math.sin(-Rotation);
            float c = math.cos(-Rotation);
            return new float2(worldVector.x * c - worldVector.y * s, worldVector.x * s + worldVector.y * c);
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
            AppliedForcesThisStep.Add((shipLocalPoint, force));
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
