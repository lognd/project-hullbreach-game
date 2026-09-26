using UnityEngine;
using Unity.Mathematics;
using Hullbreach.World;

namespace Hullbreach.Game
{
    // Without this a scene ship starts at rest and falls into the
    // planet; see the reference page.
    // frob:doc docs/reference/hullbreach-game.md#orbitstarter
    [RequireComponent(typeof(ShipController))]
    public sealed class OrbitStarter : MonoBehaviour
    {
        [SerializeField] ShipController ship;

        [SerializeField] int bodyIndex = 0;

        void Awake()
        {
            if (ship == null) ship = GetComponent<ShipController>();
        }

        void Start()
        {
            if (ship == null) return;

            var field = GravityWorld.Field;
            if (field == null) return;

            Vector3 here = transform.position;
            var velocity = OrbitHelper.CircularOrbitVelocity(field, bodyIndex, new float2(here.x, here.y));
            ship.ResetTo(new Vector2(here.x, here.y), new Vector2(velocity.x, velocity.y));
        }
    }
}
