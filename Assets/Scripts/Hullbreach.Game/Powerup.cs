using UnityEngine;
using Unity.Mathematics;

namespace Hullbreach.Game
{
    // Always created by PowerupSpawner.Spawn, which calls Configure
    // immediately after AddComponent: never place one in a scene directly,
    // since it has nothing to show/apply until configured.
    // frob:doc docs/reference/hullbreach-game.md#powerup
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class Powerup : MonoBehaviour
    {
        [SerializeField] float spinDegreesPerSecond = 90f;

        // frob:doc docs/reference/hullbreach-game.md#powerup
        public PowerupPreset Preset { get; private set; }

        bool _configured;

        // frob:doc docs/reference/hullbreach-game.md#powerup
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
