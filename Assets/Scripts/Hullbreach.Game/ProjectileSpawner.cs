using UnityEngine;
using Hullbreach.Ship;

namespace Hullbreach.Game
{
    /// <summary>
    /// Subscribes to every ShipController's ShotFired in the scene and turns
    /// each ShotRequest into a real Projectile GameObject. The Inspector
    /// fields below are written into every ship's ShipBody.Projectile at
    /// Start, so one spawner tunes every ship's cannon uniformly for the demo.
    /// </summary>
    public sealed class ProjectileSpawner : MonoBehaviour
    {
        [SerializeField] float speed = 20f;
        [SerializeField] float impulse = 2f;
        [SerializeField] byte damage = 25;
        [SerializeField] float lifetime = 3f;
        [SerializeField] float radius = 0.1f;

        ShipController[] _ships;

        void Start()
        {
            _ships = FindObjectsByType<ShipController>(FindObjectsSortMode.None);
            if (_ships.Length == 0)
            {
                Debug.LogError("ProjectileSpawner found no ShipControllers in the scene.");
            }

            var spec = new ProjectileSpec(speed, impulse, damage, lifetime, radius);
            foreach (var ship in _ships)
            {
                if (ship.Ship != null) ship.Ship.Projectile = spec;
                ship.ShotFired += OnShotFired;
            }
        }

        void OnDestroy()
        {
            if (_ships == null) return;
            foreach (var ship in _ships)
            {
                if (ship != null) ship.ShotFired -= OnShotFired;
            }
        }

        void OnShotFired(ShotRequest shot)
        {
            var go = new GameObject("Projectile");
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            go.AddComponent<CircleCollider2D>();
            var projectile = go.AddComponent<Projectile>();

            ShipCollider owner = FindOwner(shot);
            Rigidbody2D ownerBody = owner != null ? owner.GetComponent<Rigidbody2D>() : null;
            Vector2 carrierVelocity = ownerBody != null ? ownerBody.linearVelocity : Vector2.zero;

            projectile.Configure(
                new Vector2(shot.WorldOrigin.x, shot.WorldOrigin.y),
                new Vector2(shot.WorldDirection.x, shot.WorldDirection.y),
                carrierVelocity,
                shot.Spec,
                owner);
        }

        ShipCollider FindOwner(ShotRequest shot)
        {
            // The firing ship is whichever ship's grid still contains the
            // firing cannon's key with the muzzle world origin close to
            // where that ship's LocalToWorld would place it. Simpler and
            // robust enough for the demo's two ships: match by nearest ship
            // position, since ships are never coincident.
            ShipCollider best = null;
            float bestDist = float.MaxValue;
            foreach (var ship in _ships)
            {
                if (ship.Ship == null) continue;
                float dist = Vector2.Distance(new Vector2(ship.Ship.Position.x, ship.Ship.Position.y),
                                               new Vector2(shot.WorldOrigin.x, shot.WorldOrigin.y));
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = ship.GetComponent<ShipCollider>();
                }
            }
            return best;
        }
    }
}
