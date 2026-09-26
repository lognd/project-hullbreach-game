using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.World;
using Hullbreach.Ship.Behaviours;

namespace Hullbreach.Ship
{
    // PLAIN C# ON PURPOSE: no UnityEngine anywhere in this assembly. That
    // buys edit-mode tests that run in milliseconds without a scene,
    // Burst-compilable hot paths, and a simulation the headless server (S47)
    // can run without Unity's object model. ShipController is a thin adapter
    // holding lifecycle and Inspector wiring, and nothing else.
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

        // Null for none (e.g. RocketScene, which never wires one up). Set
        // by the caller (ShipController.Awake) rather than owned here, so
        // the same ShipBody can be dropped into a field-less test without a
        // stub.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public GravityField Gravity;

        // Defaults to NullWorldSink so a ShipBody built by a test (or the
        // headless server with no game layer wired up yet) never needs a
        // null check to Step. Set by the caller (ShipController.Awake).
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

        // Without it nothing ever stops a ship spinning: fins apply a
        // torque, and when the player lets go the ship keeps the rotation
        // rate it reached forever, so aiming means counter-steering
        // exactly, which is what made the demo feel uncontrollable.
        // Defaults to 0 so ShipBody's own unit tests (and any caller that
        // wants pure Newtonian rotation) are unaffected; the demo's
        // ShipController sets it from the Inspector.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float AngularDamping = 0f;

        // Added to a planet's Radius when testing block contact, so a
        // block's own half-extent does not sink visibly into the surface
        // before contact registers.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float ContactClearance = 0.5f;

        // Cleared and repopulated every Step, for the renderer/audio to
        // react to.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public readonly List<(int key, float2 normal, float speed)> ContactsThisStep = new List<(int, float2, float)>();

        // Rebuilt when topology is dirty. A dense typed list, because
        // systems iterate "all thrusters" rather than dispatching
        // polymorphically over all blocks.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public int[] ThrusterKeys = Array.Empty<int>();

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public int[] RetroKeys = Array.Empty<int>();

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public int[] FinKeys = Array.Empty<int>();

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public int[] WeaponKeys = Array.Empty<int>();

        // Deliberately the simplest possible hand-off: a flag, not an
        // event, because nothing downstream exists yet to justify more
        // machinery.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public bool FireRequested { get; private set; }

        // ShipBody never spawns projectile objects itself: it only records
        // intent and applies its own recoil. Left for the caller
        // (ShipController) to drain and clear.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public readonly List<ShotRequest> PendingShots = new List<ShotRequest>();

        // Cleared at the start of every Step. A later structural system
        // (ShipStructure) feeds these straight into StructuralSolver.Tick
        // without ShipBody needing to know StructuralSolver exists.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public readonly List<(float2 point, float2 force)> AppliedForcesThisStep = new List<(float2, float2)>();

        // Exposed (not just consumed internally) because the FE
        // inertia-relief work on another branch needs the same a = F/M this
        // integrator already computed.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float2 LastLinearAcceleration { get; private set; }

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float LastAngularAcceleration { get; private set; }

        float2 _forceAccum;
        float _torqueAccum;

        // Keyed by grid key: a cell has exactly one block and therefore
        // exactly one behaviour, so keys never collide across behaviours.
        readonly Dictionary<int, float> _throttleByKey = new Dictionary<int, float>();

        readonly Dictionary<int, float> _cannonCooldownByKey = new Dictionary<int, float>();

        // Ticked down every Step; a block reverts (variant bits cleared)
        // once its entry hits zero.
        readonly Dictionary<int, float> _powerupExpiryByKey = new Dictionary<int, float>();

        // O(block count); called lazily from Step only when
        // Grid.TopologyDirty, so placing or removing blocks is what pays
        // this cost, not every physics tick. Per-key ramp/cooldown state is
        // pruned to the surviving keys but otherwise preserved, so placing
        // an unrelated block does not reset an in-progress throttle ramp.
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

        // One aggregate read by BOTH the HUD bars and the play-mode tests,
        // so what a player sees and what a test asserts on can never drift
        // apart. Zero when the ship has no thrusters.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float ForwardThrottleMean { get; private set; }

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float ReverseThrottleMean { get; private set; }

        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float SteerThrottleMean { get; private set; }

        // Allocation-free: it walks the already-built key arrays and the
        // dictionary, so this runs on the hot path without adding GC
        // pressure.
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

        // Never call this from Update: the physics step runs on a fixed
        // timer, and applying force per rendered frame makes a 144 Hz
        // machine fly differently from a 60 Hz one, and both differently
        // from the headless server.
        //
        // Forces/torques are accumulated in SHIP-LOCAL space (thrusters and
        // fins are fixed to the hull), then the net force is rotated into
        // world space before integrating: torque is a scalar about the
        // out-of-plane axis and is unaffected by that rotation.
        //
        // Three ramped control channels drive everything: forward (target
        // 1 while ThrustAxis > 0), reverse (target 1 while ThrustAxis < 0)
        // and steer (target Steer, -1..1). Each thruster, retro and fin
        // block ramps its OWN throttle toward its channel's target at a
        // rate from its own upgrade bits, so releasing a key fades the
        // effect out rather than cutting it instantly.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
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

            // After the behaviours have ramped their per-block throttles and
            // before anything reads them, so the HUD bars and the play-mode
            // tests both see THIS tick's control state rather than last
            // tick's.
            RecomputeThrottleMeans();

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

            // Applied to the NEW angular velocity, as an exponential decay
            // rather than a subtraction, so it is stable at any dt and can
            // never drive the spin through zero and back the other way.
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

        // Each block's own weight m_i * g(worldCenter_i) is pushed through
        // AddForceAtPoint at that block's ship-local center, so it is both
        // torque-correct (a lopsided ship spins under a tidal gradient) and
        // recorded in AppliedForcesThisStep for the structural solver.
        void ApplyGravityForces()
        {
            if (Gravity == null) return;

            // Sorted key order (not Grid.All) so per-block force application
            // is deterministic regardless of the grid's internal dictionary
            // layout, and so this loop shares the same allocation-free key
            // view as ResolvePlanetContacts.
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

        // Tests every block center against Gravity for surface contact,
        // pushes the ship out of the deepest single penetration once, then
        // resolves each contacting block's normal velocity with a
        // restitution impulse (ApplyImpulseAtWorldPoint), bleeds tangential
        // velocity by Friction (a simple ground-friction approximation),
        // and applies contact damage proportional to the impact speed
        // above ContactDamageSpeed. Contacts are processed in sorted key
        // order so the result is deterministic regardless of the grid's
        // internal dictionary iteration order.
        void ResolvePlanetContacts(float dt)
        {
            ContactsThisStep.Clear();
            if (Gravity == null || Grid.Mass.Total <= 0f) return;

            // Grid.SortedKeys is already the deterministic, allocation-free
            // key view BlockGrid maintains, so there is nothing left for the
            // per-step scratch list to do; ContactKeys is kept below only
            // as a scratch buffer for anything that still needs a List<int>.
            var keys = Grid.SortedKeys;

            // Pass 1: find the single deepest penetration across all
            // contacting blocks and push the whole ship out along that
            // normal once, so multiple simultaneously-contacting blocks
            // (e.g. a flat hull landing) do not get pushed out repeatedly.
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

        // This is the entire dispatch: a new weapon or thruster variant
        // needs no change here, only a new IBlockBehaviour class and a
        // BehaviourRegistry.Register call. Keys with no resolved behaviour
        // (should not happen for ThrusterKeys/RetroKeys/FinKeys/WeaponKeys,
        // which are only ever populated with types that have one) are
        // skipped rather than throwing, so a mid-migration gap fails soft.
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

        // Shared by every ramped-throttle behaviour (forward/retro thrust,
        // fin steer, seeking thrust): one block belongs to exactly one
        // behaviour, so keys never collide.
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

        // Thin public wrapper over the private rotation helper the rest of
        // Step already uses, for behaviours that compute a world-space
        // muzzle direction from a ship-local facing.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float2 RotateLocalToWorld(float2 v) => RotateByRotation(v);

        static float RampToward(float current, float target, float ratePerSecond, float dt)
        {
            float maxDelta = ratePerSecond * dt;
            float diff = target - current;
            if (math.abs(diff) <= maxDelta) return target;
            return current + math.sign(diff) * maxDelta;
        }

        // Finds the nearest block of `baseTypeId` (base variant or already
        // transformed, ties broken by lowest key for determinism) to
        // `worldPoint`, sets its variant bits to `variant`, and records that
        // it should revert to variant 0 after `seconds` of further Step
        // calls. No-op if no block of that type exists. This is the whole
        // "temporary transform" mechanism a powerup pickup drives.
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

        // Reverts any block whose timer has run out back to variant 0,
        // clearing only the variant bits so facing and ramp-upgrade bits
        // are untouched. Allocation-free aside from the small scratch list
        // of expired keys, sized to how many powerups actually expire this
        // Step (almost always zero).
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

        // The vector counterpart of WorldToLocal (no translation), used to
        // fold a world-space gravity force into the ship-local force
        // accumulator that AddForceAtPoint expects, and by the seeking
        // thruster to turn a world-space "toward the enemy" direction into
        // a ship-local force direction.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float2 WorldVectorToLocal(float2 worldVector)
        {
            float s = math.sin(-Rotation);
            float c = math.cos(-Rotation);
            return new float2(worldVector.x * c - worldVector.y * s, worldVector.x * s + worldVector.y * c);
        }

        // Updates both the linear accumulator and the torque about the
        // center of mass: tau = r x F, with r measured from the CoM, and in
        // 2D r x F = r.x * F.y - r.y * F.x.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public void AddForceAtPoint(float2 shipLocalPoint, float2 force)
        {
            _forceAccum += force;
            float2 r = shipLocalPoint - Grid.Mass.CenterOfMass;
            _torqueAccum += r.x * force.y - r.y * force.x;
            AppliedForcesThisStep.Add((shipLocalPoint, force));
        }

        // dv = J/M, dw = cross(r, J)/I with r measured from the world-space
        // center of mass. Used for cannon recoil, which should feel like a
        // kick right now rather than a force integrated over the next dt.
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
