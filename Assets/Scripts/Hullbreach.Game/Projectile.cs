using UnityEngine;
using Unity.Mathematics;
using Hullbreach.Ship;
using Hullbreach.World;

namespace Hullbreach.Game
{
    /// <summary>
    /// A single spawned cannon round: a small yellow circle with a
    /// Rigidbody2D carrying it in a straight line, that applies the firing
    /// ship's own recoil-free hit (impulse + damage) to whatever ShipCollider
    /// it touches, then destroys itself. Spawned only by ProjectileSpawner --
    /// never construct one directly, since Configure must run before the
    /// first FixedUpdate.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class Projectile : MonoBehaviour
    {
        /// <summary>Seconds a freshly spawned projectile ignores its own
        /// firing ship's colliders, so it can clear the muzzle before trigger
        /// checks turn on against it.</summary>
        const float OwnerIgnoreSeconds = 0.1f;

        ProjectileSpec _spec;
        ShipCollider _owner;
        float _ownerIgnoreUntil;
        float _deathTime;
        bool _configured;

        /// <summary>Set up this projectile right after Instantiate, before
        /// any physics step runs. `direction` must already be normalized.</summary>
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

        /// <summary>
        /// Applies the ambient gravity field's acceleration to this
        /// projectile's own velocity each physics step (velocity += a * dt),
        /// exactly like any other free body in the field, and destroys the
        /// projectile the instant it reaches a planet's surface -- a round
        /// that hits a planet does not bounce or linger, it is gone.
        /// </summary>
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
                Destroy(gameObject);
            }
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

            Destroy(gameObject);
        }
    }
}
