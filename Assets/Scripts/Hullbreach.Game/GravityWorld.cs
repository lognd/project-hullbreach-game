using System;
using UnityEngine;
using Unity.Mathematics;
using Hullbreach.World;

namespace Hullbreach.Game
{
    // One planet/moon authored in the Inspector; see the reference page.
    // frob:doc docs/reference/hullbreach-game.md#planetspec
    [Serializable]
    public struct PlanetSpec
    {
        public Vector2 position;
        [Tooltip("Gravitational strength (G times mass). Bigger pulls harder.")]
        public float mu;
        public float radius;
        public Color color;
        public Sprite sprite;
        public float spriteScale;

        [Tooltip("How many multiples of Radius the pull stays softened " +
                 "(no blow-up near the center) before falling back to the " +
                 "ordinary inverse-square law. The actual soft radius is " +
                 "also floored at GravityBody.MinSoftRadius (2 units), so " +
                 "small wells still soften over a perceptible distance.")]
        public float softRadiusFactor;

        public PlanetSpec(Vector2 position, float mu, float radius, Color color, Sprite sprite, float spriteScale)
            : this(position, mu, radius, color, Hullbreach.World.GravityBody.DefaultSoftRadiusFactor, sprite, spriteScale)
        {
        }

        public PlanetSpec(Vector2 position, float mu, float radius, Color color, float softRadiusFactor, Sprite sprite, float spriteScale)
        {
            this.position = position;
            this.mu = mu;
            this.radius = radius;
            this.color = color;
            this.sprite = sprite;
            this.spriteScale = spriteScale;
            this.softRadiusFactor = softRadiusFactor;
        }
    }

    // Scene-level gravity setup, exposed as a static singleton;
    // see the reference page for the load-order contract.
    // frob:doc docs/reference/hullbreach-game.md#gravityworld
    [DefaultExecutionOrder(-200)]
    public sealed class GravityWorld : MonoBehaviour
    {
        [SerializeField] PlanetSpec[] planets = Array.Empty<PlanetSpec>();

        // A single tunable rather than per-planet, since the demo has no
        // need yet for a bouncy moon next to a sticky one.
        [SerializeField] float surfaceRestitution = 0.2f;

        // Null before any GravityWorld's Awake runs, or in a scene with no
        // GravityWorld at all (e.g. RocketScene).
        // frob:doc docs/reference/hullbreach-game.md#gravityworld
        public static GravityField Field { get; private set; }

        void Awake()
        {
            var field = new GravityField();
            foreach (var planet in planets)
            {
                // A freshly-resized array serializes softRadiusFactor as 0,
                // so treat <= 0 as "use the default factor".
                float factor = planet.softRadiusFactor > 0f
                    ? planet.softRadiusFactor
                    : GravityBody.DefaultSoftRadiusFactor;
                float softRadius = math.max(planet.radius * factor, GravityBody.MinSoftRadius);

                field.Add(new GravityBody(new float2(planet.position.x, planet.position.y),
                                           planet.mu, planet.radius, surfaceRestitution, softRadius));
                SpawnDisc(planet);
            }
            Field = field;
        }

        void OnDestroy()
        {
            // Only clear the static if we are the instance that set it,
            // so tearing down an unrelated one cannot blank a live field.
            if (ReferenceEquals(Field, null)) return;
            Field = null;
        }

        void SpawnDisc(PlanetSpec planet)
        {
            var go = new GameObject($"Planet_{planet.position}");
            go.transform.SetParent(transform, false);
            go.transform.position = planet.position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = planet.sprite;//ShipRenderer.MakeSprite();
            sr.color = planet.color;
            //will have to adjust scale here to match sprite size
            //11.5 for default knob graphic
            sr.transform.localScale = new Vector3(planet.radius * planet.spriteScale, planet.radius * planet.spriteScale, 1f);
            sr.sortingOrder = -10;
        }
    }
}
