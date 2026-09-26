using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;

namespace Hullbreach.Game
{
    // A plain struct because Phase A only needs "a ship exists to
    // fly", not a builder UI yet.
    // frob:doc docs/reference/hullbreach-game.md#authoredblock
    [Serializable]
    public struct AuthoredBlock
    {
        public int x;
        public int y;
        public byte typeId;
        public byte modifiers;

        public AuthoredBlock(int x, int y, byte typeId, byte modifiers = 0)
        {
            this.x = x;
            this.y = y;
            this.typeId = typeId;
            this.modifiers = modifiers;
        }
    }

    // The MonoBehaviour adapter. Lifecycle and Inspector wiring ONLY;
    // see the reference page for what this replaces and why.
    // frob:doc docs/reference/hullbreach-game.md#shipcontroller
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class ShipController : MonoBehaviour
    {
        [SerializeField] Rigidbody2D body;

        // [SerializeField] on a private field: visible in the Inspector
        // without becoming public API.
        [SerializeField] Transform[] thrusterMounts;

        // Tuning knobs mirrored onto ShipBody in Awake.
        [Header("Tuning")]
        [Tooltip("Force each forward thruster applies at full throttle.")]
        [SerializeField] float thrustPerBlock = 10f;
        [Tooltip("Force each retro thruster applies at full throttle.")]
        [SerializeField] float retroThrustPerBlock = 5f;
        [Tooltip("Force each fin applies at full steer.")]
        [SerializeField] float finForce = 6f;
        [Tooltip("Seconds a cannon waits between shots.")]
        [SerializeField] float cannonCooldown = 0.35f;
        [Tooltip("Per-second exponential decay on spin, so releasing steer settles the ship.")]
        [SerializeField] float angularDamping = 1.5f;

        // The default lays out a minimal flyable ship: core, hull,
        // two thrusters, one cannon.
        [SerializeField]
        AuthoredBlock[] blocks = new[]
        {
            new AuthoredBlock(0, 0, BlockTypes.Core),
            new AuthoredBlock(0, 1, BlockTypes.Hull),
            new AuthoredBlock(0, -1, BlockTypes.Hull),
            new AuthoredBlock(-1, -1, BlockTypes.Thruster, 0),
            new AuthoredBlock(1, -1, BlockTypes.Thruster, 0),
            new AuthoredBlock(0, 2, BlockTypes.Cannon),
        };

        ShipBody ship;

        // Exposed so the demo scene branch can read state without
        // pass-through properties for everything.
        // frob:doc docs/reference/hullbreach-game.md#shipcontroller
        public ShipBody Ship => ship;

        // Gate for player input, set by DemoMode when switching to
        // Build mode. Defaults to true (existing scenes behave as before).
        // frob:doc docs/reference/hullbreach-game.md#shipcontroller
        public bool InputEnabled = true;

        // Gate for the SIMULATION itself, distinct from InputEnabled.
        // See the reference page (the "Tab teleports me" bug).
        // frob:doc docs/reference/hullbreach-game.md#shipcontroller
        public bool SimulationEnabled = true;

        // Defaults to the legacy Input Manager bindings; a play-mode
        // test swaps in a ScriptedDemoInput.
        // frob:doc docs/reference/hullbreach-game.md#shipcontroller
        public IDemoInput InputSource = LegacyDemoInput.Instance;

        // Raised once per PendingShots entry drained in FixedUpdate,
        // so the caller can spawn a projectile without prefab knowledge here.
        // frob:doc docs/reference/hullbreach-game.md#shipcontroller
        public event Action<ShotRequest> ShotFired;

        // Latched input, written in Update and consumed in FixedUpdate.
        float thrustAxis;
        bool fireLatched;
        float steerAxis;

        void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();

            // Take ownership of mass; see the reference page.
            body.useAutoMass = false;

            ship = new ShipBody
            {
                ThrustPerBlock = thrustPerBlock,
                RetroThrustPerBlock = retroThrustPerBlock,
                FinForce = finForce,
                CannonCooldown = cannonCooldown,
                AngularDamping = angularDamping,
            };
            foreach (var b in blocks)
            {
                int key = BlockKey.Pack(b.x, b.y);
                ship.Grid.TryAdd(key, new Block(b.typeId, b.modifiers));
            }
            // Force the initial view build now rather than on the first Step,
            // so mass/CoM are already valid for the very first FixedUpdate.
            ship.RebuildDerivedViews();

            // Null-safe: ShipBody.Step treats a null Gravity as
            // "no gravity" rather than requiring a stub.
            ship.Gravity = GravityWorld.Field;

            // WorldSink.Instance is set by its own earlier-ordered Awake;
            // see the reference page.
            if (WorldSink.Instance != null) ship.World = WorldSink.Instance;
        }

        void Update()
        {
            if (!InputEnabled)
            {
                // Do not let a stale latched fire from before the ship was
                // frozen carry over into the next time it flies.
                thrustAxis = 0f;
                steerAxis = 0f;
                fireLatched = false;
                return;
            }

            var input = InputSource ?? LegacyDemoInput.Instance;

            // Level-triggered axes can be read straight through; EDGE-
            // triggered input must be latched or FixedUpdate will miss it.
            thrustAxis = input.ThrustAxis;
            steerAxis = input.Steer;
            if (input.FirePressed) fireLatched = true;
        }

        void FixedUpdate()
        {
            if (!SimulationEnabled)
            {
                // Paused (Build mode): do not integrate and do not touch the
                // Rigidbody2D, so resuming is exactly where we left off.
                fireLatched = false;
                return;
            }

            var input = InputEnabled ? new ShipInput(thrustAxis, steerAxis, fireLatched) : new ShipInput(0f, 0f, false);
            fireLatched = false;   // consume exactly once

            ship.Step(input, Time.fixedDeltaTime);

            // Drain PendingShots: ShipBody only records intent, spawning
            // the projectile is the caller's job.
            for (int i = 0; i < ship.PendingShots.Count; i++)
            {
                ShotFired?.Invoke(ship.PendingShots[i]);
            }
            ship.PendingShots.Clear();

            // Push mass properties every tick; only meaningful on a DYNAMIC
            // body. See the reference page for why kinematic bodies skip it.
            if (body.bodyType == RigidbodyType2D.Dynamic)
            {
                var mass = ship.Grid.Mass;
                body.mass = mass.Total;
                var com = mass.CenterOfMass;
                body.centerOfMass = new Vector2(com.x, com.y);
                body.inertia = mass.InertiaAboutCenterOfMass;
            }

            // ShipBody is authoritative for ship motion; see the reference
            // page for why Rigidbody2D follows it via Move*.
            body.linearVelocity = new Vector2(ship.Velocity.x, ship.Velocity.y);
            body.angularVelocity = math.degrees(ship.AngularVelocity);
            body.MovePosition(new Vector2(ship.Position.x, ship.Position.y));
            body.MoveRotation(math.degrees(ship.Rotation));
        }

        // World-space wrapper over ShipBody.ApplyImpulseAtWorldPoint.
        // frob:doc docs/reference/hullbreach-game.md#shipcontroller
        public void ApplyImpulse(Vector2 worldPoint, Vector2 impulse)
            => ship.ApplyImpulseAtWorldPoint(new float2(worldPoint.x, worldPoint.y),
                                              new float2(impulse.x, impulse.y));

        // World-space wrapper over ShipBody.ApplyDamageAtWorldPoint.
        // frob:doc docs/reference/hullbreach-game.md#shipcontroller
        public void ApplyDamage(Vector2 worldPoint, byte damage)
            => ship.ApplyDamageAtWorldPoint(new float2(worldPoint.x, worldPoint.y), damage, out _);

        // Lets a play-mode test fly a SHAPE the demo scene does not author;
        // see the reference page.
        // frob:doc docs/reference/hullbreach-game.md#shipcontroller
        public void ReplaceBlocks(IReadOnlyList<AuthoredBlock> newBlocks)
        {
            var existing = new List<int>();
            var live = ship.Grid.SortedKeys;
            for (int i = 0; i < live.Length; i++) existing.Add(live[i]);
            foreach (int key in existing) ship.Grid.TryRemove(key);

            for (int i = 0; i < newBlocks.Count; i++)
            {
                var b = newBlocks[i];
                ship.Grid.TryAdd(BlockKey.Pack(b.x, b.y), new Block(b.typeId, b.modifiers));
            }
            ship.RebuildDerivedViews();

            var shipRenderer = GetComponent<ShipRenderer>();
            if (shipRenderer != null) shipRenderer.MarkDirty();
            var shipCollider = GetComponent<ShipCollider>();
            if (shipCollider != null) shipCollider.MarkDirty();
            var structure = GetComponent<ShipStructure>();
            if (structure != null) structure.Solver.MarkTopologyChanged();
        }

        // Lets a caller bind fire to a key not wired to "Fire1", e.g. Space.
        // frob:doc docs/reference/hullbreach-game.md#shipcontroller
        public void RequestFire()
        {
            if (InputEnabled) fireLatched = true;
        }

        // Used by DemoMode's R key so a mangled or drifted ship can be
        // brought back for another pass.
        // frob:doc docs/reference/hullbreach-game.md#shipcontroller
        public void ResetToOrigin()
        {
            ship.Position = float2.zero;
            ship.Rotation = 0f;
            ship.Velocity = float2.zero;
            ship.AngularVelocity = 0f;

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.MovePosition(Vector2.zero);
                body.MoveRotation(0f);
            }
        }

        // Used by DemoMode's R key when startInOrbit is set.
        // frob:doc docs/reference/hullbreach-game.md#shipcontroller
        public void ResetTo(Vector2 position, Vector2 velocity)
        {
            ship.Position = new float2(position.x, position.y);
            ship.Rotation = 0f;
            ship.Velocity = new float2(velocity.x, velocity.y);
            ship.AngularVelocity = 0f;

            if (body != null)
            {
                body.linearVelocity = velocity;
                body.angularVelocity = 0f;
                body.MovePosition(position);
                body.MoveRotation(0f);
            }
        }

        // Draws each authored block plus the center of mass, so the
        // ship is visible in the editor without sprites.
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            var localBlocks = blocks;
            for (int i = 0; i < localBlocks.Length; i++)
            {
                var b = localBlocks[i];
                var center = transform.TransformPoint(new Vector3(b.x + 0.5f, b.y + 0.5f, 0f));
                var size = transform.TransformVector(new Vector3(BlockType.Width, BlockType.Height, 0f));
                Gizmos.DrawWireCube(center, size);
            }

            if (ship != null && ship.Grid.Mass.Total > 0f)
            {
                Gizmos.color = Color.yellow;
                var com = ship.Grid.Mass.CenterOfMass;
                Gizmos.DrawWireSphere(transform.TransformPoint(new Vector3(com.x, com.y, 0f)), 0.15f);
            }
        }
    }
}
