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
            if (!OrbitHelper.TryCircularOrbitVelocity(field, bodyIndex, new float2(here.x, here.y), out var velocity))
            {
                Debug.LogWarning($"OrbitStarter: no circular orbit around body {bodyIndex} from {here}; leaving the ship where it is.", this);
                return;
            }
            ship.ResetTo(new Vector2(here.x, here.y), new Vector2(velocity.x, velocity.y));
        }
    }
}
