using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.World;
using Hullbreach.Ship.Behaviours;

namespace Hullbreach.Ship
{
    // PLAIN C# ON PURPOSE: no UnityEngine anywhere, so this stays testable
    // and portable to the headless server (S47).
    // frob:doc docs/reference/hullbreach-ship.md#shipbody
    public sealed class ShipBody
    {
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public readonly BlockGrid Grid = new BlockGrid();

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float2 Position;
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float Rotation;
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float2 Velocity;
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float AngularVelocity;

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float ThrustPerBlock = 10f;

        // Defaults to half of ThrustPerBlock: retros are two small side
        // nozzles, not a main engine.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float RetroThrustPerBlock = 5f;

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float FinForce = 6f;

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float CannonCooldown = 0.35f;

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public ProjectileSpec Projectile = ProjectileSpec.Default;

        // Null for none (e.g. RocketScene). Set by the caller
        // (ShipController.Awake) rather than owned here.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public GravityField Gravity;

        // Defaults to NullWorldSink so a ShipBody built by a test never
        // needs a null check to Step.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public IWorldSink World = NullWorldSink.Instance;

        // Below this, a landing is "soft" and does no damage at all.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float ContactDamageSpeed = 3f;

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float ContactDamagePerSpeed = 12f;

        // Approximates ground friction without a full friction-cone solve.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float Friction = 2f;

        // Without it nothing stops a ship spinning once steer is released.
        // Defaults to 0 so tests get pure Newtonian rotation.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float AngularDamping = 0f;

        // Added to a planet's Radius when testing block contact, so a
        // block's half-extent does not visibly sink into the surface.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float ContactClearance = 0.5f;

        // Cleared and repopulated every Step, for the renderer/audio.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public readonly List<(int key, float2 normal, float speed)> ContactsThisStep = new List<(int, float2, float)>();

        // Rebuilt when topology is dirty; systems iterate "all thrusters"
        // rather than dispatching polymorphically over all blocks.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public int[] ThrusterKeys = Array.Empty<int>();

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public int[] RetroKeys = Array.Empty<int>();

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public int[] FinKeys = Array.Empty<int>();

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public int[] WeaponKeys = Array.Empty<int>();

        // Deliberately the simplest possible hand-off: a flag, not an
        // event, since nothing downstream needs more machinery yet.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public bool FireRequested { get; private set; }

        // ShipBody never spawns projectiles itself; left for the caller
        // (ShipController) to drain and clear.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public readonly List<ShotRequest> PendingShots = new List<ShotRequest>();

        // Cleared at the start of every Step. A later structural system
        // feeds these into StructuralSolver.Tick without ShipBody knowing.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public readonly List<(float2 point, float2 force)> AppliedForcesThisStep = new List<(float2, float2)>();

        // Exposed because the FE inertia-relief work on another branch
        // needs the same a = F/M this integrator already computed.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float2 LastLinearAcceleration { get; private set; }

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float LastAngularAcceleration { get; private set; }

        float2 _forceAccum;
        float _torqueAccum;

        // Keyed by grid key: a cell has exactly one block/behaviour, so
        // keys never collide.
        readonly Dictionary<int, float> _throttleByKey = new Dictionary<int, float>();

        readonly Dictionary<int, float> _cannonCooldownByKey = new Dictionary<int, float>();

        // Ticked down every Step; reverts (variant bits cleared) at zero.
        readonly Dictionary<int, float> _powerupExpiryByKey = new Dictionary<int, float>();

        // O(block count); called lazily only when Grid.TopologyDirty.
        // Per-key ramp/cooldown state is pruned but otherwise preserved.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
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

            PruneStale(_throttleByKey, ThrusterKeys, RetroKeys, FinKeys);
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

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float Throttle(int key) => _throttleByKey.TryGetValue(key, out var t) ? t : 0f;

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float SteerThrottle(int key) => _throttleByKey.TryGetValue(key, out var t) ? t : 0f;

        // One aggregate read by BOTH the HUD bars and the play-mode tests.
        // Zero when the ship has no thrusters.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float ForwardThrottleMean { get; private set; }

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float ReverseThrottleMean { get; private set; }

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float SteerThrottleMean { get; private set; }

        // Allocation-free: walks the already-built key arrays and the
        // dictionary, so this stays off the GC on the hot path.
        void RecomputeThrottleMeans()
        {
            ForwardThrottleMean = MeanThrottle(ThrusterKeys);
            ReverseThrottleMean = MeanThrottle(RetroKeys);
            SteerThrottleMean = MeanThrottle(FinKeys);
        }

        float MeanThrottle(int[] keys)
        {
            if (keys.Length == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < keys.Length; i++)
            {
                if (_throttleByKey.TryGetValue(keys[i], out float t)) sum += t;
            }
            return sum / keys.Length;
        }

        // Never call this from Update: runs on a fixed timer so behavior
        // does not depend on frame rate. See docs/reference/hullbreach-ship.md#shipbody.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public void Step(in ShipInput input, float dt)
        {
            if (Grid.TopologyDirty) RebuildDerivedViews();

            AppliedForcesThisStep.Clear();
            FireRequested = input.FirePressed;

            float mass = Grid.Mass.Total;
            if (mass <= 0f)
            {
                // No blocks left: nothing to push, so skip integration
                // rather than divide by zero.
                LastLinearAcceleration = float2.zero;
                LastAngularAcceleration = 0f;
                ForwardThrottleMean = 0f;
                ReverseThrottleMean = 0f;
                SteerThrottleMean = 0f;
                return;
            }

            TickPowerups(dt);

            StepBehaviours(ThrusterKeys, input, dt);
            StepBehaviours(RetroKeys, input, dt);
            StepBehaviours(FinKeys, input, dt);
            StepBehaviours(WeaponKeys, input, dt);

            // After behaviours ramp their throttles, before anything reads
            // them, so readers see THIS tick's control state.
            RecomputeThrottleMeans();

            ApplyGravityForces();

            float inertia = Grid.Mass.InertiaAboutCenterOfMass;

            float2 worldForce = RotateByRotation(_forceAccum);
            float2 a = worldForce / mass;
            float alpha = inertia > 0f ? _torqueAccum / inertia : 0f;

            LastLinearAcceleration = a;
            LastAngularAcceleration = alpha;

            // Semi-implicit (symplectic) Euler: update velocity first, then
            // use the NEW velocity to update position (as Box2D does).
            Velocity += a * dt;
            AngularVelocity += alpha * dt;

            // Exponential decay on the NEW angular velocity: stable at any
            // dt, never drives the spin through zero and back.
            if (AngularDamping > 0f)
            {
                AngularVelocity *= 1f / (1f + AngularDamping * dt);
            }
            Position += Velocity * dt;
            Rotation += AngularVelocity * dt;

            _forceAccum = float2.zero;
            _torqueAccum = 0f;

            ResolvePlanetContacts(dt);
        }

        // Each block's own weight is pushed through AddForceAtPoint at its
        // ship-local center, so a lopsided ship spins under a tidal gradient.
        void ApplyGravityForces()
        {
            if (Gravity == null) return;

            // Sorted key order (not Grid.All) so this is deterministic and
            // shares the same allocation-free key view as ResolvePlanetContacts.
            var keys = Grid.SortedKeys;
            for (int i = 0; i < keys.Length; i++)
            {
                int key = keys[i];
                if (!Grid.TryGet(key, out Block block)) continue;
                float2 localCenter = BlockGrid.CenterOf(key);
                float2 worldCenter = LocalToWorld(localCenter);
                float2 accel = Gravity.AccelerationAt(worldCenter);
                float blockMass = BlockTypes.Get(block.TypeId).Mass;
                float2 worldForce = accel * blockMass;
                float2 localForce = WorldVectorToLocal(worldForce);
                AddForceAtPoint(localCenter, localForce);
            }
        }

        // Tests every block against Gravity for contact and applies
        // push-out, restitution/friction impulses and contact damage.
        void ResolvePlanetContacts(float dt)
        {
            ContactsThisStep.Clear();
            if (Gravity == null || Grid.Mass.Total <= 0f) return;

            // Grid.SortedKeys is already the deterministic, allocation-free
            // key view BlockGrid maintains.
            var keys = Grid.SortedKeys;

            // Pass 1: find the single deepest penetration and push the
            // whole ship out once, so a flat landing is not pushed out repeatedly.
            float deepestPenetration = 0f;
            float2 deepestNormal = float2.zero;
            bool anyContact = false;

            for (int i = 0; i < keys.Length; i++)
            {
                int key = keys[i];
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

            for (int i = 0; i < keys.Length; i++)
            {
                int key = keys[i];
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

                // Ground friction: bleed the tangential velocity,
                // recomputed after restitution so friction acts post-bounce.
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

        // The entire dispatch: a new variant needs only an IBlockBehaviour
        // class; missing behaviours are skipped rather than throwing.
        void StepBehaviours(int[] keys, in ShipInput input, float dt)
        {
            foreach (int key in keys)
            {
                if (!Grid.TryGet(key, out var block)) continue;
                var behaviour = BehaviourRegistry.Resolve(block);
                if (behaviour == null) continue;

                float2 localCenter = BlockGrid.CenterOf(key);
                var ctx = new BlockContext
                {
                    Ship = this,
                    Key = key,
                    Block = block,
                    LocalCenter = localCenter,
                    WorldCenter = LocalToWorld(localCenter),
                    Facing = Hullbreach.Core.Facing.Direction(block.Modifiers),
                    Dt = dt,
                    Input = input,
                    World = World ?? NullWorldSink.Instance,
                };
                behaviour.Step(ref ctx);
            }
        }

        // Shared by every ramped-throttle behaviour; one block belongs to
        // exactly one behaviour, so keys never collide.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float RampThrottleFor(int key, byte modifiers, float target, float dt)
        {
            float rate = ThrusterUpgrades.RampRate(modifiers);
            float current = _throttleByKey.TryGetValue(key, out var t) ? t : 0f;
            float updated = RampToward(current, target, rate, dt);
            _throttleByKey[key] = updated;
            return updated;
        }

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float TickCooldownFor(int key, float dt)
        {
            float remaining = _cannonCooldownByKey.TryGetValue(key, out var rem) ? rem : 0f;
            remaining = math.max(0f, remaining - dt);
            _cannonCooldownByKey[key] = remaining;
            return remaining;
        }

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public void SetCooldownFor(int key, float seconds) => _cannonCooldownByKey[key] = seconds;

        // Thin public wrapper over the private rotation helper Step uses,
        // for a behaviour's world-space muzzle direction.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float2 RotateLocalToWorld(float2 v) => RotateByRotation(v);

        static float RampToward(float current, float target, float ratePerSecond, float dt)
        {
            float maxDelta = ratePerSecond * dt;
            float diff = target - current;
            if (math.abs(diff) <= maxDelta) return target;
            return current + math.sign(diff) * maxDelta;
        }

        // Finds the nearest block of `baseTypeId`, sets its variant bits,
        // and schedules a revert after `seconds` (the powerup mechanism).
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public bool ApplyPowerup(byte variant, byte baseTypeId, float2 worldPoint, float seconds)
        {
            float2 localPoint = WorldToLocal(worldPoint);

            int bestKey = -1;
            float bestDistSq = float.PositiveInfinity;
            var candidateKeys = new List<int>();
            foreach (var kv in Grid.All)
            {
                if (kv.Value.TypeId != baseTypeId) continue;
                candidateKeys.Add(kv.Key);
            }
            candidateKeys.Sort();

            foreach (int key in candidateKeys)
            {
                float2 center = BlockGrid.CenterOf(key);
                float distSq = math.distancesq(center, localPoint);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestKey = key;
                }
            }

            if (bestKey == -1) return false;
            if (!Grid.TryGet(bestKey, out var block)) return false;

            byte newModifiers = BlockVariants.With(block.Modifiers, variant);
            if (!Grid.TrySet(bestKey, block.WithModifiers(newModifiers))) return false;

            _powerupExpiryByKey[bestKey] = seconds;
            return true;
        }

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float VariantTimeLeft(int key)
            => _powerupExpiryByKey.TryGetValue(key, out var remaining) ? remaining : 0f;

        // Reverts any block whose timer ran out to variant 0, clearing only
        // the variant bits (facing/ramp-upgrade bits untouched).
        void TickPowerups(float dt)
        {
            if (_powerupExpiryByKey.Count == 0) return;

            List<int> expired = null;
            var keys = new List<int>(_powerupExpiryByKey.Keys);
            foreach (int key in keys)
            {
                float remaining = _powerupExpiryByKey[key] - dt;
                if (remaining <= 0f)
                {
                    (expired ??= new List<int>()).Add(key);
                }
                else
                {
                    _powerupExpiryByKey[key] = remaining;
                }
            }

            if (expired == null) return;
            foreach (int key in expired)
            {
                _powerupExpiryByKey.Remove(key);
                if (Grid.TryGet(key, out var block))
                {
                    byte revertedModifiers = BlockVariants.With(block.Modifiers, 0);
                    Grid.TrySet(key, block.WithModifiers(revertedModifiers));
                }
            }
        }

        float2 RotateByRotation(float2 v)
        {
            float s = math.sin(Rotation);
            float c = math.cos(Rotation);
            return new float2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float2 LocalToWorld(float2 localPoint) => RotateByRotation(localPoint) + Position;

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float2 WorldToLocal(float2 worldPoint)
        {
            float2 d = worldPoint - Position;
            float s = math.sin(-Rotation);
            float c = math.cos(-Rotation);
            return new float2(d.x * c - d.y * s, d.x * s + d.y * c);
        }

        // The vector counterpart of WorldToLocal (no translation): folds a
        // world-space force/direction into ship-local space.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float2 WorldVectorToLocal(float2 worldVector)
        {
            float s = math.sin(-Rotation);
            float c = math.cos(-Rotation);
            return new float2(worldVector.x * c - worldVector.y * s, worldVector.x * s + worldVector.y * c);
        }

        // Updates both the linear accumulator and the torque about the
        // CoM: tau = r x F = r.x*F.y - r.y*F.x in 2D.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public void AddForceAtPoint(float2 shipLocalPoint, float2 force)
        {
            _forceAccum += force;
            float2 r = shipLocalPoint - Grid.Mass.CenterOfMass;
            _torqueAccum += r.x * force.y - r.y * force.x;
            AppliedForcesThisStep.Add((shipLocalPoint, force));
        }

        // dv = J/M, dw = cross(r, J)/I about the world-space CoM. Used for
        // cannon recoil, which should feel instant, not integrated.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
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

        // `key` is the grid key checked (valid even on a miss, -1 only if
        // the point falls outside the packable grid range).
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
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

    // A value type, so it is trivially serializable for the netcode and
    // trivially constructible in tests.
    // frob:doc docs/reference/hullbreach-ship.md#shipinput
    public readonly struct ShipInput
    {
        // Positive fires forward thrusters, negative fires retro
        // thrusters, zero fires neither.
        // frob:doc docs/reference/hullbreach-ship.md#shipinput
        public readonly float ThrustAxis;

        // frob:doc docs/reference/hullbreach-ship.md#shipinput
        public readonly float Steer;

        // Edge, not level: true only the tick the button goes down.
        // frob:doc docs/reference/hullbreach-ship.md#shipinput
        public readonly bool FirePressed;

        // frob:doc docs/reference/hullbreach-ship.md#shipinput
        public ShipInput(float thrustAxis, float steer, bool firePressed)
        {
            ThrustAxis = thrustAxis;
            Steer = steer;
            FirePressed = firePressed;
        }
    }
}
