using UnityEngine;
using Unity.Mathematics;
using Hullbreach.Ship;

namespace Hullbreach.Game
{
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

            // TODO [A0]: Construct the ShipBody and populate its grid from the
            //            authored ship. Until Phase A is green, keep
            //            PlayerSingle on the prefab instead of this.
            ship = new ShipBody();
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

            // TODO [A5]: ship.Step(input, Time.fixedDeltaTime), then push the
            //            result onto the Rigidbody2D -- or, once the custom
            //            integrator lands (S47), drive the transform directly
            //            and stop using Rigidbody2D for ship motion entirely.

            // TODO [A3]: Push mass properties from the grid accumulators:
            //            body.mass, body.centerOfMass, body.inertia.
        }
    }
}
