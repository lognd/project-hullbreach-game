using System.Collections.Generic;
using UnityEngine;
using Hullbreach.Ship;

namespace Hullbreach.Game
{
    // The Inspector fields are written into every ship's
    // ShipBody.Projectile at Start; see the reference page.
    // frob:doc docs/reference/hullbreach-game.md#projectilespawner
    public sealed class ProjectileSpawner : MonoBehaviour
    {
        [SerializeField] float speed = 20f;
        [SerializeField] float impulse = 2f;
        [SerializeField] byte damage = 25;
        [SerializeField] float lifetime = 3f;
        [SerializeField] float radius = 0.1f;

        // Initialized inline so SpawnFromSink is safe before Start.
        readonly List<ShipController> _ships = new List<ShipController>();

        void Start()
        {
            Refresh();
            if (_ships.Count == 0)
            {
                Debug.LogError("ProjectileSpawner found no ShipControllers in the scene.");
            }
        }

        // Wires every ship in the scene that is not wired yet (idempotent);
        // call after spawning a ship at runtime. WorldSink.Refresh does.
        // frob:doc docs/reference/hullbreach-game.md#projectilespawner
        public void Refresh()
        {
            foreach (var ship in FindObjectsByType<ShipController>(FindObjectsSortMode.None))
                Register(ship);
        }

        // Applies the Inspector spec to the ship and subscribes to its shots;
        // a repeat call for the same ship is a no-op.
        // frob:doc docs/reference/hullbreach-game.md#projectilespawner
        public void Register(ShipController ship)
        {
            if (ship == null || _ships.Contains(ship)) return;
            _ships.Add(ship);
            if (ship.Ship != null)
                ship.Ship.Projectile = new ProjectileSpec(speed, impulse, damage, lifetime, radius);
            ship.ShotFired += OnShotFired;
        }

        void OnDestroy()
        {
            foreach (var ship in _ships)
            {
                if (ship != null) ship.ShotFired -= OnShotFired;
            }
        }

        void OnShotFired(ShotRequest shot) => SpawnFromSink(shot);

        // Public so a block behaviour's ShotRequest (e.g. the gravity
        // gun) can share this spawn path; see the reference page.
        // frob:doc docs/reference/hullbreach-game.md#projectilespawner
        public void SpawnFromSink(ShotRequest shot)
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
            // Attributes a shot to the nearest ship position; see the
            // reference page.
            ShipCollider best = null;
            float bestDist = float.MaxValue;
            foreach (var ship in _ships)
            {
                if (ship == null || ship.Ship == null) continue;
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
