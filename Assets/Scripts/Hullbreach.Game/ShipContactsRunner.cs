using System.Collections.Generic;
using UnityEngine;
using Hullbreach.Ship;

namespace Hullbreach.Game
{
    // Ordered AFTER ShipController (-100) and ShipStructure (-50); see
    // the reference page for the full ordering rationale.
    // frob:doc docs/reference/hullbreach-game.md#shipcontactsrunner
    [DefaultExecutionOrder(-40)]
    public sealed class ShipContactsRunner : MonoBehaviour
    {
        readonly List<ShipController> _ships = new List<ShipController>();
        float _rescanTimer;

        // Ships are not created every frame, so polling this rarely
        // costs nothing.
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
