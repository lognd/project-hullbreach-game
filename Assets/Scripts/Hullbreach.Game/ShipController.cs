using System;
using UnityEngine;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;

namespace Hullbreach.Game
{
    /// <summary>
    /// One block of the Inspector-authored ship, before it goes into the
    /// grid. A plain serializable struct rather than a ScriptableObject or
    /// prefab-per-ship, because Phase A only needs "a ship exists to fly",
    /// not a builder UI (that is Hullbreach.Builder's job later).
    /// </summary>
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

    /// <summary>
    /// The MonoBehaviour adapter. Lifecycle and Inspector wiring ONLY -- every
    /// line of actual simulation belongs in ShipBody, which has no UnityEngine
    /// dependency and is therefore testable in edit mode, Burst-compilable, and
    /// runnable on the headless server.
    ///
    /// This replaces PlayerSingle. Two things it fixes:
    ///
    ///  1. Forces move from Update to FixedUpdate. Update runs once per RENDERED
    ///     frame with a variable delta; the 2D physics step runs on a fixed
    ///     timer. Applying force from Update makes acceleration depend on
    ///     framerate.
    ///
    ///  2. Input is LATCHED. Input.GetButtonDown is true for exactly one
    ///     rendered frame, so polling it from FixedUpdate misses presses
    ///     outright. Read edge-triggered input in Update, store it, consume it
    ///     in FixedUpdate.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class ShipController : MonoBehaviour
    {
        [SerializeField] Rigidbody2D body;

        // [SerializeField] on a private field is the idiomatic choice: visible
        // and editable in the Inspector without becoming public API. Note Unity
        // serializes FIELDS only -- a property would not show up at all.
        [SerializeField] Transform[] thrusterMounts;

        /// <summary>The ship's blocks, authored in the Inspector until the
        /// builder (Hullbreach.Builder) can construct ships at runtime. The
        /// default lays out a minimal flyable ship: a core, hull fore/aft, two
        /// thrusters at the wingtips facing up, and a cannon up front.</summary>
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

        // Latched input, written in Update and consumed in FixedUpdate.
        bool thrustHeld;
        bool fireLatched;
        float steerAxis;

        void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();

            // Take ownership of mass. useAutoMass recomputes from collider
            // geometry on every change, which is slower and gives no control
            // over the value the netcode has to agree on.
            body.useAutoMass = false;

            ship = new ShipBody();
            foreach (var b in blocks)
            {
                int key = BlockKey.Pack(b.x, b.y);
                ship.Grid.TryAdd(key, new Block(b.typeId, b.modifiers));
            }
            // Force the initial view build now rather than on the first Step,
            // so mass/CoM are already valid for the very first FixedUpdate.
            ship.RebuildDerivedViews();
        }

        void Update()
        {
            // TODO [A5]: Migrate to the new Input System alongside S27
            //            (rebindable keys). activeInputHandler is currently 2
            //            ("Both"), so the legacy calls still work -- but every
            //            one of these lines breaks the moment that changes.
            thrustHeld = Input.GetButton("Jump");
            steerAxis = Input.GetAxis("Horizontal");

            // Level-triggered input can be read directly; EDGE-triggered input
            // must be latched or FixedUpdate will miss it.
            if (Input.GetButtonDown("Fire1")) fireLatched = true;
        }

        void FixedUpdate()
        {
            var input = new ShipInput(thrustHeld, steerAxis, fireLatched);
            fireLatched = false;   // consume exactly once

            ship.Step(input, Time.fixedDeltaTime);

            // Push mass properties from the grid accumulators every tick:
            // adding/removing a block (combat damage, later builder edits)
            // changes these, and TopologyDirty already gates the expensive
            // part (RebuildDerivedViews) inside Step, so this is cheap insurance.
            var mass = ship.Grid.Mass;
            body.mass = mass.Total;
            var com = mass.CenterOfMass;
            body.centerOfMass = new Vector2(com.x, com.y);
            body.inertia = mass.InertiaAboutCenterOfMass;

            // ShipBody is authoritative for ship motion: it is the plain C#
            // sim that the headless server (S47) will run too, so the
            // Rigidbody2D must follow it rather than the other way around.
            // MovePosition/MoveRotation (not transform.position) keep this
            // compatible with Rigidbody2D's interpolation and with other
            // colliders still resolving contacts against it; velocity is set
            // directly so ricochets/collisions read a physically consistent
            // rigidbody even though ShipBody, not Box2D, is doing the
            // integrating.
            body.linearVelocity = new Vector2(ship.Velocity.x, ship.Velocity.y);
            body.angularVelocity = math.degrees(ship.AngularVelocity);
            body.MovePosition(new Vector2(ship.Position.x, ship.Position.y));
            body.MoveRotation(math.degrees(ship.Rotation));
        }

        /// <summary>
        /// Draws each authored block as a wire square in ship-local space,
        /// plus the current center of mass, so the ship is visible in the
        /// editor without needing sprites yet.
        /// </summary>
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
