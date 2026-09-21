using System;
using UnityEngine;
using Unity.Mathematics;
using Hullbreach.World;

namespace Hullbreach.Game
{
    /// <summary>
    /// One planet/moon authored in the Inspector: its GravityBody plus the
    /// color used for its runtime-generated disc, so the scene needs no
    /// baked sprites to show planets.
    /// </summary>
    [Serializable]
    public struct PlanetSpec
    {
        public Vector2 position;
        [Tooltip("Gravitational strength (G times mass). Bigger pulls harder.")]
        public float mu;
        public float radius;
        public Color color;

        public PlanetSpec(Vector2 position, float mu, float radius, Color color)
        {
            this.position = position;
            this.mu = mu;
            this.radius = radius;
            this.color = color;
        }
    }

    /// <summary>
    /// Scene-level gravity setup: builds a single GravityField from the
    /// Inspector-authored PlanetSpec list on Awake and exposes it as a
    /// static singleton so ShipController and Projectile can pick it up
    /// without a scene-graph reference. Also spawns one visible disc per
    /// planet, since the field itself is invisible plain data.
    ///
    /// "Singleton-ish": Field is null until some GravityWorld's Awake has
    /// run, and DemoMode/ShipController read it lazily (null-safe) rather
    /// than requiring load order: there is exactly one GravityWorld in
    /// any scene that uses gravity, same convention as the rest of the demo.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class GravityWorld : MonoBehaviour
    {
        [SerializeField] PlanetSpec[] planets = Array.Empty<PlanetSpec>();

        /// <summary>Restitution used for every planet's surface bounce.
        /// A single tunable rather than per-planet, since the demo has no
        /// need yet for a bouncy moon next to a sticky one.</summary>
        [SerializeField] float surfaceRestitution = 0.2f;

        /// <summary>The live field this scene's ships and projectiles pull
        /// from. Null before any GravityWorld's Awake runs, or in a scene
        /// with no GravityWorld at all (e.g. RocketScene).</summary>
        public static GravityField Field { get; private set; }

        void Awake()
        {
            var field = new GravityField();
            foreach (var planet in planets)
            {
                field.Add(new GravityBody(new float2(planet.position.x, planet.position.y),
                                           planet.mu, planet.radius, surfaceRestitution));
                SpawnDisc(planet);
            }
            Field = field;
        }

        void OnDestroy()
        {
            // Only clear the static if we are the GravityWorld that set it,
            // so tearing down a second, unrelated instance (e.g. during
            // scene-transition tests) cannot blank out a still-live field.
            if (ReferenceEquals(Field, null)) return;
            Field = null;
        }

        void SpawnDisc(PlanetSpec planet)
        {
            var go = new GameObject($"Planet_{planet.position}");
            go.transform.SetParent(transform, false);
            go.transform.position = planet.position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ShipRenderer.MakeSprite();
            sr.color = planet.color;
            sr.transform.localScale = new Vector3(planet.radius * 2f, planet.radius * 2f, 1f);
            sr.sortingOrder = -10;
        }
    }
}
