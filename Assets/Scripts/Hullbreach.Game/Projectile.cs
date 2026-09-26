using UnityEngine;
using Unity.Mathematics;
using Hullbreach.Ship;
using Hullbreach.World;

namespace Hullbreach.Game
{
    // Spawned only by ProjectileSpawner: never construct one directly,
    // since Configure must run before the first FixedUpdate.
    // frob:doc docs/reference/hullbreach-game.md#projectile
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class Projectile : MonoBehaviour
    {
        // Lets a freshly spawned projectile clear the muzzle before trigger
        // checks turn on against its own firing ship's colliders.
        const float OwnerIgnoreSeconds = 0.1f;

        ProjectileSpec _spec;
        ShipCollider _owner;
        float _ownerIgnoreUntil;
        float _deathTime;
        bool _configured;

        // `direction` must already be normalized.
        // frob:doc docs/reference/hullbreach-game.md#projectile
        public void Configure(Vector2 origin, Vector2 direction, Vector2 carrierVelocity, ProjectileSpec spec, ShipCollider owner)
        {
            _spec = spec;
            _owner = owner;
            _ownerIgnoreUntil = Time.time + OwnerIgnoreSeconds;
            _deathTime = Time.time + spec.LifetimeSeconds;
            _configured = true;

            transform.position = origin;

            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = ShipRenderer.MakeSprite();
            sr.color = Color.yellow;
            transform.localScale = new Vector3(spec.Radius * 2f, spec.Radius * 2f, 1f);

            var body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.linearVelocity = direction * spec.Speed + carrierVelocity;

            var col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f; // world radius = 0.5 * localScale, set above

            if (_owner != null)
            {
                foreach (var kvp in _owner.ColliderToKey)
                {
                    Physics2D.IgnoreCollision(col, kvp.Key, true);
                }
            }
        }

        void Update()
        {
            if (!_configured) return;
            if (Time.time >= _deathTime) Destroy(gameObject);
        }

        // A round that hits a planet does not bounce or linger, it is gone.
        void FixedUpdate()
        {
            if (!_configured) return;

            var field = GravityWorld.Field;
            if (field == null) return;

            var body = GetComponent<Rigidbody2D>();
            Vector2 position = transform.position;
            float2 worldPos = new float2(position.x, position.y);
            float2 accel = field.AccelerationAt(worldPos);
            body.linearVelocity += new Vector2(accel.x, accel.y) * Time.fixedDeltaTime;

            if (field.TryContact(worldPos, _spec.Radius, out _, out _, out _))
            {
                DropWellIfAny(worldPos);
                Destroy(gameObject);
            }
        }

        // No-op for a plain (Kind == None) round, and safely no-op if no
        // WorldSink exists.
        void DropWellIfAny(Vector2 worldPoint)
        {
            if (_spec.Kind != ProjectileKind.GravityWell) return;
            var well = _spec.Well;
            var body = new GravityBody(new float2(worldPoint.x, worldPoint.y), well.Mu, well.Radius, 0f);
            WorldSink.Instance?.AddTemporaryGravity(body, well.Seconds);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!_configured) return;

            var targetCollider = other.GetComponentInParent<ShipCollider>();
            if (targetCollider == null) return;
            if (targetCollider == _owner && Time.time < _ownerIgnoreUntil) return;
            if (targetCollider == _owner) return; // never damage the firing ship, even after the grace window

            var targetController = targetCollider.GetComponent<ShipController>();
            if (targetController == null) return;

            Vector2 hitPoint = transform.position;
            Vector2 direction = GetComponent<Rigidbody2D>().linearVelocity.normalized;

            targetController.ApplyImpulse(hitPoint, direction * _spec.Impulse);
            targetController.ApplyDamage(hitPoint, _spec.Damage);
            DropWellIfAny(hitPoint);

            Destroy(gameObject);
        }
    }
}
