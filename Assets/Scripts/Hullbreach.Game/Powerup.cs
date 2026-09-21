using UnityEngine;
using Unity.Mathematics;

namespace Hullbreach.Game
{
    /// <summary>
    /// A floating pickup: a spinning tinted disc with a trigger collider.
    /// On contact with a ship it calls that ship's ShipBody.ApplyPowerup at
    /// the pickup's own position, transforming the nearest block of
    /// Preset.baseTypeId into Preset.variant for Preset.seconds, then
    /// destroys itself (its PowerupSpawner, if any, respawns it after a
    /// delay). Always created by PowerupSpawner.Spawn, which calls
    /// Configure immediately after AddComponent -- never place one in a
    /// scene directly, since it has nothing to show/apply until configured.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class Powerup : MonoBehaviour
    {
        /// <summary>Degrees per second this powerup's sprite spins, purely
        /// cosmetic (makes it read as "alive" from a distance).</summary>
        [SerializeField] float spinDegreesPerSecond = 90f;

        /// <summary>The preset this instance was spawned from, kept so
        /// PowerupSpawner can respawn an identical one after collection.</summary>
        public PowerupPreset Preset { get; private set; }

        bool _configured;

        /// <summary>Applies `preset`'s color/label to this instance. Must be
        /// called once, right after AddComponent&lt;Powerup&gt;.</summary>
        public void Configure(PowerupPreset preset)
        {
            Preset = preset;
            _configured = true;

            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = ShipRenderer.MakeSprite();
            sr.color = preset.color;

            var col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
        }

        void Update()
        {
            transform.Rotate(0f, 0f, spinDegreesPerSecond * Time.deltaTime);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!_configured) return;

            var shipCollider = other.GetComponentInParent<ShipCollider>();
            if (shipCollider == null) return;

            var controller = shipCollider.GetComponent<ShipController>();
            if (controller == null || controller.Ship == null) return;

            Vector2 point = transform.position;
            bool applied = controller.Ship.ApplyPowerup(Preset.variant, Preset.baseTypeId,
                new float2(point.x, point.y), Preset.seconds);
            if (!applied) return;

            GetComponentInParent<PowerupSpawner>()?.NotifyCollected(this);
            Destroy(gameObject);
        }
    }
}
