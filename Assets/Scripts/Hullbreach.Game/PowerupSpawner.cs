using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Hullbreach.Core;

namespace Hullbreach.Game
{
    // The disc itself is generated at runtime (ShipRenderer.MakeSprite),
    // same convention as GravityWorld's planets.
    // frob:doc docs/reference/hullbreach-game.md#poweruppreset
    [Serializable]
    public struct PowerupPreset
    {
        public Vector2 position;
        public byte variant;
        public byte baseTypeId;
        public float seconds;
        public Color color;
        public string displayName;
        public float radius;

        public PowerupPreset(Vector2 position, byte variant, byte baseTypeId, float seconds,
                              Color color, string displayName, float radius = 0.6f)
        {
            this.position = position;
            this.variant = variant;
            this.baseTypeId = baseTypeId;
            this.seconds = seconds;
            this.color = color;
            this.displayName = displayName;
            this.radius = radius;
        }
    }

    // Respawns one at the same place after respawnSeconds once it is
    // collected: this lets the demo scene keep a fixed lineup of pickups
    // visible near the player's orbit start without hand-placing prefabs.
    // frob:doc docs/reference/hullbreach-game.md#powerupspawner
    public sealed class PowerupSpawner : MonoBehaviour
    {
        [SerializeField] PowerupPreset[] presets = Array.Empty<PowerupPreset>();
        [SerializeField] float respawnSeconds = 10f;

        void Start()
        {
            foreach (var preset in presets) Spawn(preset);
        }

        void Spawn(PowerupPreset preset)
        {
            var go = new GameObject($"Powerup_{preset.displayName}");
            go.transform.SetParent(transform, false);
            go.transform.position = preset.position;
            go.transform.localScale = new Vector3(preset.radius * 2f, preset.radius * 2f, 1f);

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f; // world radius = 0.5 * localScale, set above

            var powerup = go.AddComponent<Powerup>();
            powerup.Configure(preset);
        }

        // No-op for a Powerup this spawner did not create (defensive;
        // should not happen since Powerup only looks up its own parent
        // spawner).
        // frob:doc docs/reference/hullbreach-game.md#powerupspawner
        public void NotifyCollected(Powerup collected)
        {
            StartCoroutine(RespawnAfterDelay(collected.Preset));
        }

        IEnumerator RespawnAfterDelay(PowerupPreset preset)
        {
            yield return new WaitForSeconds(respawnSeconds);
            Spawn(preset);
        }
    }
}
