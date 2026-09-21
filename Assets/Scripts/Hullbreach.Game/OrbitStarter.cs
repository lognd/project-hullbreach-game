using UnityEngine;
using Unity.Mathematics;
using Hullbreach.World;

namespace Hullbreach.Game
{
    /// <summary>
    /// Puts a non-player ship onto a circular orbit around one of
    /// GravityWorld's bodies at its authored position, on the first frame.
    ///
    /// Without this a scene ship simply starts at rest inside a gravity
    /// field and falls into the planet within seconds, which is why the demo
    /// scene's target ship was parked far from the action: the only place it
    /// could survive was somewhere nothing else was happening. The orbit
    /// velocity is derived from the transform, so moving the ship in the
    /// Inspector can never leave a stale velocity behind.
    /// </summary>
    [RequireComponent(typeof(ShipController))]
    public sealed class OrbitStarter : MonoBehaviour
    {
        [SerializeField] ShipController ship;

        /// <summary>Index of the GravityWorld body to orbit.</summary>
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
