using System.Collections.Generic;
using UnityEngine;
using Hullbreach.Ship;

namespace Hullbreach.Game
{
    /// <summary>
    /// Runs <see cref="ShipContacts.Resolve"/> over every pair of ships in
    /// the scene once per FixedUpdate.
    ///
    /// Ordered AFTER ShipController (-100) and ShipStructure (-50) so it acts
    /// on the positions this tick's integration produced, and its
    /// corrections land before the next tick rather than a frame late. Ship
    /// rigidbodies are kinematic (ShipBody is the only integrator), so
    /// nothing else in the scene is resolving these overlaps.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class ShipContactsRunner : MonoBehaviour
    {
        readonly List<ShipController> _ships = new List<ShipController>();
        float _rescanTimer;

        /// <summary>Seconds between rescans of the scene for ships. Ships are
        /// not created every frame, so polling this rarely costs nothing and
        /// avoids a registration protocol for the demo's fixed cast.</summary>
        const float RescanSeconds = 1f;

        void Awake() => Rescan();

        void FixedUpdate()
        {
            _rescanTimer -= Time.fixedDeltaTime;
            if (_rescanTimer <= 0f) Rescan();

            for (int i = 0; i < _ships.Count; i++)
            {
                for (int j = i + 1; j < _ships.Count; j++)
                {
                    var a = _ships[i];
                    var b = _ships[j];
                    if (a == null || b == null || a.Ship == null || b.Ship == null) continue;

                    // A paused ship (Build mode) is not part of the
                    // simulation this tick, so it must not be pushed either.
                    if (!a.SimulationEnabled || !b.SimulationEnabled) continue;

                    ShipContacts.Resolve(a.Ship, b.Ship, Time.fixedDeltaTime);
                }
            }
        }

        void Rescan()
        {
            _rescanTimer = RescanSeconds;
            _ships.Clear();
            _ships.AddRange(Object.FindObjectsByType<ShipController>(FindObjectsSortMode.InstanceID));
        }
    }
}
