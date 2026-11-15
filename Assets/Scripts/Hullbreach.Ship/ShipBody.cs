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

        // How the ship feels to fly: flight assist, ramps, brake, fire buffer. Legacy by default so raw physics
        // tests stay pure; the server, the client prediction and ShipController all assign FlightTuning.Default().
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public FlightTuning Tuning = FlightTuning.Legacy();

        // Velocity the brake assist kills relative to; the dominant body's velocity. Planets are static, so zero.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float2 BrakeReferenceVelocity;

        // Seconds left on a buffered fire press (a press during cooldown still fires when the cannon is ready).
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float FireBufferRemaining => _fireBuffer;

        // True while the gyro is holding or commanding the spin this tick.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public bool AssistActive { get; private set; }

        float _fireBuffer;

        // Added to a planet's Radius when testing block contact, so a
        // block's half-extent does not visibly sink into the surface.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float ContactClearance = 0.5f;

        // Planet crash, atmosphere and warning tunables; the server and the client prediction must share one value.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public PlanetCrashConfig PlanetCrash = PlanetCrashConfig.Default();

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

        // Farthest block corner from the ship origin; lets Step skip substepping while far from every planet.
        float _reach = 1f;

        // Fractional heating damage per block, carried so slow burns are not rounded away.
        readonly Dictionary<int, float> _heatCarry = new Dictionary<int, float>();

        // Scratch for one contact pass; reused so Step stays allocation-free.
        readonly List<int> _contactKeys = new List<int>();
        readonly List<float2> _contactNormals = new List<float2>();
        readonly List<float> _contactClosing = new List<float>();
        readonly List<float> _contactRestitution = new List<float>();
        readonly List<float> _contactImpulse = new List<float>();
        readonly List<int> _contactBodies = new List<int>();

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

            // A powerup timer must not outlive its block, or it would revert
            // an unrelated block later placed at the same key.
            var removed = new List<int>();
            foreach (int key in _powerupExpiryByKey.Keys)
                if (!Grid.Contains(key)) removed.Add(key);
            foreach (int key in removed) _powerupExpiryByKey.Remove(key);

            float reach = 1f;
            foreach (int key in Grid.SortedKeys)
                reach = math.max(reach, math.length(BlockGrid.CenterOf(key)) + 0.71f);
            _reach = reach;

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

        // Carries ramped throttles over from another body (for blocks still here), so a rebuilt
        // prediction body does not restart every thruster from zero.
        // frob:ticket 01M6WV815GYEJ58MTM4HSS4Z6K
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public void CopyThrottlesFrom(ShipBody other)
        {
            foreach (var kv in other._throttleByKey)
                if (Grid.Contains(kv.Key)) _throttleByKey[kv.Key] = kv.Value;
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
                // Nothing integrates, so drop stale per-step outputs and
                // externally added loads rather than leaking them to a later ship.
                ContactsThisStep.Clear();
                _forceAccum = float2.zero;
                _torqueAccum = 0f;
                ForwardThrottleMean = 0f;
                ReverseThrottleMean = 0f;
                SteerThrottleMean = 0f;
                return;
            }

            TickPowerups(dt);

            // A fire press is remembered briefly so one that lands during cooldown still fires.
            if (input.FirePressed) _fireBuffer = Tuning.FireBufferSeconds;
            bool fireNow = input.FirePressed || _fireBuffer > 0f;
            int shotsBefore = PendingShots.Count;
            ShipInput driven = new ShipInput(input.ThrustAxis, input.Steer, fireNow, input.AssistOff);

            StepBehaviours(ThrusterKeys, driven, dt);
            StepBehaviours(RetroKeys, driven, dt);
            StepBehaviours(FinKeys, driven, dt);
            StepBehaviours(WeaponKeys, driven, dt);

            _fireBuffer = PendingShots.Count > shotsBefore ? 0f : math.max(0f, _fireBuffer - dt);

            // After behaviours ramp their throttles, before anything reads
            // them, so readers see THIS tick's control state.
            RecomputeThrottleMeans();

            ApplyGravityForces();

            float inertia = Grid.Mass.InertiaAboutCenterOfMass;
            ApplyGyro(input, mass, inertia, dt);

            float2 worldForce = RotateByRotation(_forceAccum);
            float2 a = worldForce / mass;
            float alpha = inertia > 0f ? _torqueAccum / inertia : 0f;

            LastLinearAcceleration = a;
            LastAngularAcceleration = alpha;

            // Semi-implicit (symplectic) Euler: update velocity first, then
            // use the NEW velocity to update position (as Box2D does).
            Velocity += a * dt;
            AngularVelocity += alpha * dt;
            ApplyBrake(input, mass, dt);

            // Exponential decay on the NEW angular velocity: stable at any
            // dt, never drives the spin through zero and back. The gyro replaces it while assist is on.
            if (AngularDamping > 0f && !AssistActive)
            {
                AngularVelocity *= 1f / (1f + AngularDamping * dt);
            }

            _forceAccum = float2.zero;
            _torqueAccum = 0f;

            ContactsThisStep.Clear();
            if (Gravity == null)
            {
                Position += Velocity * dt;
                Rotation += AngularVelocity * dt;
                return;
            }

            ApplyAtmosphere(dt);

            // Fast ships move in several short hops with a contact test after each, so a block
            // can never skip across the surface layer in one tick.
            int substeps = SweepSubsteps(dt);
            float h = dt / substeps;
            for (int s = 0; s < substeps; s++)
            {
                Position += Velocity * h;
                Rotation += AngularVelocity * h;
                ResolvePlanetContacts(h);
            }
        }

        // Flight assist: steer commands a spin rate (capped by ship inertia) and a gyro torque tracks it, so a
        // released stick holds the heading. The torque is proportional to ship mass and never overshoots the
        // target within one step, so it is stable at any tick rate.
        void ApplyGyro(in ShipInput input, float mass, float inertia, float dt)
        {
            AssistActive = Tuning.AssistEnabled && !input.AssistOff && inertia > 0f;
            if (!AssistActive) return;

            float target = math.clamp(input.Steer, -1f, 1f) * Tuning.MaxRateFor(inertia);
            float predicted = AngularVelocity + _torqueAccum / inertia * dt;
            float error = target - predicted;
            if (error == 0f) return;

            float alpha = Tuning.GyroTorquePerMass * mass / inertia * math.clamp(error / Tuning.GyroErrorSaturation, -1f, 1f);
            float limit = math.abs(error) / dt;
            alpha = math.clamp(alpha, -limit, limit);
            _torqueAccum += alpha * inertia;
        }

        // Brake assist: while S is held, an extra deceleration against the velocity relative to the reference body.
        // It scales with the retro thrusters' own acceleration and their spool-up, and stops the ship rather than
        // reversing it. Ships without a retro thruster get nothing.
        void ApplyBrake(in ShipInput input, float mass, float dt)
        {
            if (Tuning.BrakeGain <= 0f || input.ThrustAxis >= -0.05f || RetroKeys.Length == 0) return;

            float retroAccel = RetroKeys.Length * RetroThrustPerBlock / mass;
            float dv = Tuning.BrakeGain * retroAccel * ReverseThrottleMean * dt;
            float2 rel = Velocity - BrakeReferenceVelocity;
            float speed = math.length(rel);
            if (speed <= 0f) return;
            if (speed <= Tuning.BrakeStopSpeed || dv >= speed) { Velocity = BrakeReferenceVelocity; return; }
            Velocity -= rel / speed * dv;
        }

        // 1 unless a planet surface is within reach this tick; then enough hops that none exceeds SweepStep.
        int SweepSubsteps(float dt)
        {
            float travel = (math.length(Velocity) + math.abs(AngularVelocity) * _reach) * dt;
            if (travel <= PlanetCrash.SweepStep) return 1;

            bool near = false;
            int count = Gravity.PermanentCount;
            for (int i = 0; i < count && !near; i++)
            {
                if (!Gravity.TryGetPermanent(i, out var body)) continue;
                float gap = math.length(Position - body.Position) - body.Radius - ContactClearance;
                near = gap <= _reach + travel;
            }
            if (!near) return 1;

            int steps = (int)math.ceil(travel / PlanetCrash.SweepStep);
            return math.clamp(steps, 1, PlanetCrash.MaxSubsteps);
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

        // Tests every block against Gravity for contact; applies push-out, a restitution impulse
        // (none for a gentle touch, capped so no setting adds energy), friction, and crash damage
        // to exactly the blocks that touch. `h` is the substep length.
        const int ContactIterations = 6;

        void ResolvePlanetContacts(float h)
        {
            if (Grid.Mass.Total <= 0f) return;

            // Grid.SortedKeys is already the deterministic, allocation-free key view.
            var keys = Grid.SortedKeys;
            var cfg = PlanetCrash;

            // Pass 1: push the whole ship out once by the deepest penetration, so a flat landing is not pushed out repeatedly.
            float deepestPenetration = 0f;
            float2 deepestNormal = float2.zero;
            for (int i = 0; i < keys.Length; i++)
            {
                float2 center = LocalToWorld(BlockGrid.CenterOf(keys[i]));
                if (!Gravity.TryContact(center, ContactClearance, out _, out var n, out var pen)) continue;
                if (pen > deepestPenetration) { deepestPenetration = pen; deepestNormal = n; }
            }
            if (deepestPenetration <= 0f) return;
            Position += deepestNormal * deepestPenetration;

            float mass = Grid.Mass.Total;
            float inertia = Grid.Mass.InertiaAboutCenterOfMass;
            float2 comWorld = LocalToWorld(Grid.Mass.CenterOfMass);

            // Pass 2: snapshot every contact BEFORE any impulse, so one block impulse cannot
            // change what the next block is charged for (order independent damage).
            _contactKeys.Clear(); _contactNormals.Clear(); _contactClosing.Clear(); _contactBodies.Clear(); _contactRestitution.Clear(); _contactImpulse.Clear();
            int pressing = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                int key = keys[i];
                float2 center = LocalToWorld(BlockGrid.CenterOf(key));
                if (!Gravity.TryContact(center, ContactClearance, out int bodyIndex, out var normal, out _)) continue;
                float2 r = center - comWorld;
                float2 blockVelocity = Velocity + AngularVelocity * new float2(-r.y, r.x);
                float closing = -math.dot(blockVelocity, normal);
                _contactKeys.Add(key); _contactNormals.Add(normal); _contactClosing.Add(closing); _contactBodies.Add(bodyIndex);
                if (closing > 0f) pressing++;
            }

            // Pass 3a: normal impulses, solved together. Each contact keeps the total impulse it has pushed with
            // (never negative) and the loop repeats a few times, so a flat landing on several blocks stops dead
            // instead of one corner block kicking the whole ship back up (a single sequential pass overshoots).
            for (int c = 0; c < _contactKeys.Count; c++)
            {
                float restitution = 0f;
                if (Gravity.TryGetPermanent(_contactBodies[c], out var body))
                    restitution = math.clamp(body.SurfaceRestitution, 0f, cfg.MaxRestitution);
                restitution = cfg.EffectiveRestitution(restitution, _contactClosing[c]);
                if (_contactClosing[c] < cfg.RestSpeed) restitution = 0f;
                _contactRestitution.Add(restitution);
                _contactImpulse.Add(0f);
            }
            for (int iteration = 0; iteration < ContactIterations; iteration++)
            {
                for (int c = 0; c < _contactKeys.Count; c++)
                {
                    float2 normal = _contactNormals[c];
                    float2 worldCenter = LocalToWorld(BlockGrid.CenterOf(_contactKeys[c]));
                    float2 r = worldCenter - comWorld;
                    float2 liveVelocity = Velocity + AngularVelocity * new float2(-r.y, r.x);
                    float vn = math.dot(liveVelocity, normal);
                    float wanted = _contactClosing[c] > 0f ? _contactRestitution[c] * _contactClosing[c] : 0f;
                    float crossRN = r.x * normal.y - r.y * normal.x;
                    float denom = inertia > 0f ? (1f / mass) + (crossRN * crossRN) / inertia : (1f / mass);
                    float delta = (wanted - vn) / denom;
                    float accumulated = math.max(0f, _contactImpulse[c] + delta);
                    float applied = accumulated - _contactImpulse[c];
                    _contactImpulse[c] = accumulated;
                    if (applied != 0f) ApplyImpulseAtWorldPoint(worldCenter, normal * applied);
                }
            }

            // Pass 3b: friction and damage, block by block.
            for (int c = 0; c < _contactKeys.Count; c++)
            {
                int key = _contactKeys[c];
                float2 normal = _contactNormals[c];
                float closing = _contactClosing[c];
                float2 worldCenter = LocalToWorld(BlockGrid.CenterOf(key));
                float2 r = worldCenter - comWorld;

                // Ground friction: bleed the tangential velocity after the bounce.
                float2 tangent = new float2(-normal.y, normal.x);
                float2 postVelocity = Velocity + AngularVelocity * new float2(-r.y, r.x);
                float vt = math.dot(postVelocity, tangent);
                float frictionImpulseMag = -vt * math.clamp(Friction * h, 0f, 1f) * mass;
                if (frictionImpulseMag != 0f) ApplyImpulseAtWorldPoint(worldCenter, tangent * frictionImpulseMag);

                ContactsThisStep.Add((key, normal, math.abs(closing)));

                if (closing > cfg.SafeLandingSpeed && Grid.TryGet(key, out var block))
                {
                    float damage = cfg.ImpactDamage(closing, pressing, block.TypeId == BlockTypes.Core);
                    if (damage >= 1f)
                        Grid.TrySet(key, block.WithDamage((byte)math.min(255, block.Damage + (int)damage)));
                }
            }
        }

        // Drag and heating while fast inside a thin band above a planet surface. Velocity changes only,
        // like the arena push, so no force reaches the structural solver; the core is never heated.
        void ApplyAtmosphere(float dt)
        {
            var cfg = PlanetCrash;
            float speed = math.length(Velocity);
            if (speed <= cfg.AtmosphereSafeSpeed) return;

            float2 comWorld = LocalToWorld(Grid.Mass.CenterOfMass);
            int count = Gravity.PermanentCount;
            for (int i = 0; i < count; i++)
            {
                if (!Gravity.TryGetPermanent(i, out var body)) continue;
                speed = math.length(Velocity);
                if (speed <= cfg.AtmosphereSafeSpeed) return;

                float altitude = math.length(comWorld - body.Position) - body.Radius;
                float density = cfg.AtmosphereDensity(body, altitude);
                if (density <= 0f) continue;

                float2 direction = Velocity / speed;
                float excess = speed - cfg.AtmosphereSafeSpeed;
                float slow = math.min(cfg.AtmosphereDrag * density * excess * dt, excess);
                Velocity -= direction * slow;

                if (speed > cfg.HeatSpeed) HeatLeadingBlocks(direction, comWorld, density, speed, dt);
            }
        }

        void HeatLeadingBlocks(float2 direction, float2 comWorld, float density, float speed, float dt)
        {
            var cfg = PlanetCrash;
            float perSecond = math.min(cfg.HeatMaxDamagePerSecond,
                cfg.HeatDamagePerSecond * density * (speed - cfg.HeatSpeed) / cfg.HeatSpeed);
            if (perSecond <= 0f) return;

            var keys = Grid.SortedKeys;
            float front = float.NegativeInfinity;
            for (int i = 0; i < keys.Length; i++)
                front = math.max(front, math.dot(LocalToWorld(BlockGrid.CenterOf(keys[i])) - comWorld, direction));

            for (int i = 0; i < keys.Length; i++)
            {
                int key = keys[i];
                if (math.dot(LocalToWorld(BlockGrid.CenterOf(key)) - comWorld, direction) < front - 0.5f) continue;
                if (!Grid.TryGet(key, out var block) || block.TypeId == BlockTypes.Core) continue;

                _heatCarry.TryGetValue(key, out float carry);
                carry += perSecond * dt;
                int whole = (int)carry;
                _heatCarry[key] = carry - whole;
                if (whole > 0) Grid.TrySet(key, block.WithDamage((byte)math.min(255, block.Damage + whole)));
            }
        }

        // The nearest planet as seen from the lowest block: gap, closing speed, time to touchdown and
        // how bad it would be. Allocation-free; safe to call every frame from the HUD.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public PlanetProximity QueryPlanetProximity()
        {
            var result = new PlanetProximity
            {
                BodyIndex = -1, TimeToImpact = float.PositiveInfinity, StopDistance = float.PositiveInfinity, Severity = ImpactSeverity.None, IsSafe = true,
            };
            if (Gravity == null || Grid.Mass.Total <= 0f) return result;

            var keys = Grid.SortedKeys;
            float bestGap = float.PositiveInfinity;
            float2 bestCenter = float2.zero;
            int bestBody = -1;
            GravityBody bestBodyValue = default;
            int count = Gravity.PermanentCount;
            for (int b = 0; b < count; b++)
            {
                if (!Gravity.TryGetPermanent(b, out var body)) continue;
                for (int i = 0; i < keys.Length; i++)
                {
                    float2 center = LocalToWorld(BlockGrid.CenterOf(keys[i]));
                    float gap = math.length(center - body.Position) - body.Radius - ContactClearance;
                    if (gap < bestGap) { bestGap = gap; bestCenter = center; bestBody = b; bestBodyValue = body; }
                }
            }
            if (bestBody < 0) return result;

            var cfg = PlanetCrash;
            float2 toShip = bestCenter - bestBodyValue.Position;
            float2 normal = math.normalizesafe(toShip, new float2(0f, 1f));
            float2 comWorld = LocalToWorld(Grid.Mass.CenterOfMass);
            float2 r = bestCenter - comWorld;
            float2 velocity = Velocity + AngularVelocity * new float2(-r.y, r.x);
            float closing = -math.dot(velocity, normal);

            result.HasBody = true;
            result.BodyIndex = bestBody;
            result.SurfaceNormal = normal;
            result.Altitude = math.max(bestGap, 0f);
            result.ClosingSpeed = closing;
            result.TangentialSpeed = math.abs(math.dot(velocity, new float2(-normal.y, normal.x)));

            float comAltitude = math.length(comWorld - bestBodyValue.Position) - bestBodyValue.Radius;
            result.AtmosphereDensity = cfg.AtmosphereDensity(bestBodyValue, comAltitude);
            float speed = math.length(Velocity);
            result.Heating = result.AtmosphereDensity > 0f && speed > cfg.HeatSpeed;

            float inward = -math.dot(Gravity.AccelerationAt(bestCenter), normal);
            const float MinClosing = 0.25f;
            if (closing > MinClosing)
            {
                result.TimeToImpact = PlanetProximity.TimeToCover(result.Altitude, closing, inward, out float impact);
                result.PredictedImpactSpeed = impact;
                if (result.TimeToImpact <= cfg.WarningSeconds)
                    result.Severity = PlanetProximity.Classify(cfg, impact);

                float thrust = ThrusterKeys.Length * ThrustPerBlock / Grid.Mass.Total;
                float net = thrust - inward;
                result.StopDistance = net > 1e-3f ? closing * closing / (2f * net) : float.PositiveInfinity;
                result.MustBrake = result.Severity >= ImpactSeverity.Caution && result.StopDistance * 1.3f >= result.Altitude;
            }
            result.IsSafe = result.Severity < ImpactSeverity.Caution && !result.Heating;
            return result;
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
            float current = _throttleByKey.TryGetValue(key, out var t) ? t : 0f;
            bool up = math.abs(target) >= math.abs(current);
            float rate = ThrusterUpgrades.RampRate(modifiers) * (up ? Tuning.ThrusterRampUpScale : Tuning.ThrusterRampDownScale);
            float updated = RampToward(current, target, rate, dt);
            _throttleByKey[key] = updated;
            return updated;
        }

        // Fin ramp: quick on, quicker off, so steering is crisp. Falls back to the shared ramp when untuned.
        // frob:doc docs/reference/hullbreach-ship.md#shipbody
        public float RampFinThrottleFor(int key, byte modifiers, float target, float dt)
        {
            if (Tuning.FinRampUpSeconds <= 0f || Tuning.FinRampDownSeconds <= 0f) return RampThrottleFor(key, modifiers, target, dt);
            float current = _throttleByKey.TryGetValue(key, out var t) ? t : 0f;
            bool up = math.abs(target) >= math.abs(current);
            float rate = 1f / (up ? Tuning.FinRampUpSeconds : Tuning.FinRampDownSeconds);
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

        // Hold-to-disable flight assist: true means raw fin physics, no gyro.
        // frob:doc docs/reference/hullbreach-ship.md#shipinput
        public readonly bool AssistOff;

        // frob:doc docs/reference/hullbreach-ship.md#shipinput
        public ShipInput(float thrustAxis, float steer, bool firePressed, bool assistOff = false)
        {
            ThrustAxis = thrustAxis;
            Steer = steer;
            FirePressed = firePressed;
            AssistOff = assistOff;
        }
    }
}
