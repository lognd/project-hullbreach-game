using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Ship;

namespace Hullbreach.Game
{
    // Which per-block overlay ShipRenderer tints with, cycled by DemoMode's
    // O key.
    // frob:doc docs/reference/hullbreach-game.md#overlaymode
    public enum OverlayMode
    {
        // Plain per-type colors, no tint.
        None,
        // Green (0) to red (>=1) by max(DuctileRatio, BrittleRatio).
        Stress,
        // Magenta on articulation points, dim everywhere else.
        LoadBearing,
        // White (undamaged) to black (fully damaged) by DamageFraction.
        Damage,
        // Blue (0) to red (>=1) by BlockStress.BucklingRatio alone
        // (via ExtraRatioSource).
        Buckling,
    }

    // Builds and maintains one child GameObject per block; no prefabs.
    // See the reference page for why rebuild is push-driven, not polled.
    // frob:doc docs/reference/hullbreach-game.md#shiprenderer
    [RequireComponent(typeof(ShipController))]
    public sealed class ShipRenderer : MonoBehaviour
    {
        [SerializeField] ShipController controller;

        // frob:doc docs/reference/hullbreach-game.md#shiprenderer
        public OverlayMode Overlay = OverlayMode.None;

        // Optional extra per-block ratio hook for a later buckling
        // system. Null means "no extra source".
        // frob:doc docs/reference/hullbreach-game.md#shiprenderer
        public Func<int, float> ExtraRatioSource;

        // Set by ShipStructure once it exists; null draws Stress as
        // all-green.
        // frob:doc docs/reference/hullbreach-game.md#shiprenderer
        public Hullbreach.Structure.StructuralSolver Solver;

        // Blocks at or above this ratio pulse bright red regardless of
        // overlay. Set by DemoMode; zero or negative disables the flash.
        // frob:doc docs/reference/hullbreach-game.md#shiprenderer
        public float FlashRatioThreshold = 0.8f;

        static Sprite _unitSprite;
        bool _dirty = true;
        int _builtCount = -1;

        readonly Dictionary<int, BlockVisual> _visuals = new Dictionary<int, BlockVisual>();
        readonly HashSet<int> _articulation = new HashSet<int>();

        // Per-block child GameObjects, cached so overlay/flame updates
        // never need GetComponentInChildren.
        sealed class BlockVisual
        {
            public GameObject Root;
            public SpriteRenderer Body;
            public SpriteRenderer Nose;
            public SpriteRenderer Flame;
            public SpriteRenderer Flame2;
            public ParticleSystem Exhaust;
            public ParticleSystem Exhaust2;
            public byte TypeId;
            public byte Modifiers;
        }

        void Awake()
        {
            if (controller == null) controller = GetComponent<ShipController>();
            if (controller == null) Debug.LogError("ShipRenderer requires a ShipController on the same GameObject.");
        }

        // Call after any edit to the ship's grid (placement, removal,
        // detach) so the next LateUpdate rebuilds the visuals.
        // frob:doc docs/reference/hullbreach-game.md#shiprenderer
        public void MarkDirty() => _dirty = true;

        // Cached statically so every block/flame/projectile on every ship
        // reuses the same texture.
        // frob:doc docs/reference/hullbreach-game.md#shiprenderer
        public static Sprite MakeSprite()
        {
            if (_unitSprite != null) return _unitSprite;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.filterMode = FilterMode.Point;
            tex.Apply();

            _unitSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            _unitSprite.name = "HullbreachUnitSprite";
            return _unitSprite;
        }

        static Color ColorForType(byte typeId) => typeId switch
        {
            BlockTypes.Core => new Color(0.85f, 0.68f, 0.1f),
            BlockTypes.Hull => new Color(0.55f, 0.55f, 0.58f),
            BlockTypes.Armor => new Color(0.22f, 0.25f, 0.32f),
            BlockTypes.Thruster => new Color(0.65f, 0.35f, 0.12f),
            BlockTypes.Cannon => new Color(0.4f, 0.08f, 0.08f),
            BlockTypes.Fin => new Color(0.55f, 0.85f, 0.55f),
            BlockTypes.RetroThruster => new Color(0.1f, 0.55f, 0.55f),
            _ => Color.white,
        };

        void LateUpdate()
        {
            if (controller == null || controller.Ship == null) return;
            var grid = controller.Ship.Grid;

            if (_dirty || grid.Count != _builtCount)
            {
                Rebuild(grid);
                _dirty = false;
                _builtCount = grid.Count;
            }

            if (Overlay == OverlayMode.LoadBearing) Articulation.Compute(grid, _articulation);

            foreach (var kvp in _visuals)
            {
                UpdateFlames(kvp.Key, kvp.Value);
                UpdateOverlay(kvp.Key, kvp.Value, grid);
            }
        }

        void Rebuild(BlockGrid grid)
        {
            // Destroy visuals for keys no longer on the grid.
            var stale = new List<int>();
            foreach (var key in _visuals.Keys)
            {
                if (!grid.Contains(key)) stale.Add(key);
            }
            foreach (var key in stale)
            {
                Destroy(_visuals[key].Root);
                _visuals.Remove(key);
            }

            foreach (var kvp in grid.All)
            {
                if (_visuals.TryGetValue(kvp.Key, out var existing)
                    && existing.TypeId == kvp.Value.TypeId
                    && existing.Modifiers == kvp.Value.Modifiers)
                {
                    continue; // unchanged, keep the GameObject
                }

                if (_visuals.TryGetValue(kvp.Key, out var stale2))
                {
                    Destroy(stale2.Root);
                    _visuals.Remove(kvp.Key);
                }

                _visuals[kvp.Key] = BuildVisual(kvp.Key, kvp.Value);
            }
        }

        BlockVisual BuildVisual(int key, Block block)
        {
            BlockKey.Unpack(key, out int x, out int y);
            var root = new GameObject($"Block_{x}_{y}_{BlockTypes.Get(block.TypeId).Name}");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(x + 0.5f, y + 0.5f, 0f);

            var body = root.AddComponent<SpriteRenderer>();
            body.sprite = MakeSprite();
            body.color = ColorForType(block.TypeId);
            body.sortingOrder = 0;

            var visual = new BlockVisual { Root = root, Body = body, TypeId = block.TypeId, Modifiers = block.Modifiers };

            bool directional = block.TypeId == BlockTypes.Cannon || block.TypeId == BlockTypes.Fin
                || block.TypeId == BlockTypes.Thruster || block.TypeId == BlockTypes.RetroThruster;
            if (directional)
            {
                float2 facing = block.TypeId == BlockTypes.Thruster ? new Unity.Mathematics.float2(0f, 1f)
                    : block.TypeId == BlockTypes.RetroThruster ? new Unity.Mathematics.float2(0f, -1f)
                    : Facing.Direction(block.Modifiers);

                var nose = new GameObject("Nose").AddComponent<SpriteRenderer>();
                nose.transform.SetParent(root.transform, false);
                nose.sprite = MakeSprite();
                nose.color = Color.Lerp(ColorForType(block.TypeId), Color.white, 0.5f);
                nose.transform.localPosition = new Vector3(facing.x * 0.3f, facing.y * 0.3f, -0.01f);
                nose.transform.localScale = new Vector3(0.25f, 0.5f, 1f);
                nose.sortingOrder = 1;
                visual.Nose = nose;
            }

            if (block.TypeId == BlockTypes.Thruster)
            {
                // Forward thrust exhausts ship-local -y, so that is where the
                // red flame and its particle trail go.
                visual.Flame = BuildFlame(root.transform, new Vector3(0f, -0.5f, 0f), ForwardFlameColor);
                visual.Exhaust = BuildExhaust(root.transform, new Vector3(0f, -0.55f, 0f),
                                              new Vector2(0f, -1f), ForwardFlameColor);
            }
            else if (block.TypeId == BlockTypes.RetroThruster)
            {
                // A retro thruster exhausts FORWARD out two SIDE nozzles;
                // see the reference page for why.
                visual.Flame = BuildFlame(root.transform, new Vector3(0.5f, 0.25f, 0f), RetroFlameColor);
                visual.Flame2 = BuildFlame(root.transform, new Vector3(-0.5f, 0.25f, 0f), RetroFlameColor);
                visual.Exhaust = BuildExhaust(root.transform, new Vector3(0.5f, 0.5f, 0f),
                                              new Vector2(0f, 1f), RetroFlameColor);
                visual.Exhaust2 = BuildExhaust(root.transform, new Vector3(-0.5f, 0.5f, 0f),
                                               new Vector2(0f, 1f), RetroFlameColor);
            }
            else if (block.TypeId == BlockTypes.Fin)
            {
                visual.Flame = BuildFlame(root.transform, new Vector3(0f, -0.3f, 0f), new Color(0.7f, 0.95f, 0.7f));
                visual.Exhaust = BuildExhaust(root.transform, new Vector3(0f, -0.3f, 0f),
                                              new Vector2(0f, -1f), Color.white);
            }

            return visual;
        }

        // Forward thrusters read RED: the main drive is the loud, hot one,
        // and it must be unmistakable from the retro plumes.
        // frob:doc docs/reference/hullbreach-game.md#shiprenderer
        public static readonly Color ForwardFlameColor = new Color(1f, 0.22f, 0.06f);

        // Retro thrusters read GREEN, the opposite channel to the
        // forward drive's red; see the reference page.
        // frob:doc docs/reference/hullbreach-game.md#shiprenderer
        public static readonly Color RetroFlameColor = new Color(0.15f, 1f, 0.35f);

        // Particles per second at full throttle.
        const float ExhaustRateAtFullThrottle = 60f;

        // Particles per second for a fin's steering puff; thinner
        // than a thruster plume.
        const float FinPuffRateAtFullSteer = 18f;

        static Material _particleMaterial;

        // Returns null (caller skips particles) if the Sprites/Default
        // shader is missing from a stripped player.
        static Material ParticleMaterial()
        {
            if (_particleMaterial != null) return _particleMaterial;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("Sprites/Default shader is missing; thruster particles are disabled.");
                return null;
            }

            _particleMaterial = new Material(shader) { mainTexture = MakeSprite().texture };
            _particleMaterial.name = "HullbreachExhaustParticles";
            return _particleMaterial;
        }

        // Idle until UpdateFlames raises its emission rate. Simulation
        // space is World so the trail is left BEHIND a moving ship.
        static ParticleSystem BuildExhaust(Transform parent, Vector3 localPos, Vector2 localDirection, Color color)
        {
            var material = ParticleMaterial();
            if (material == null) return null;

            var go = new GameObject("Exhaust");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            // A cone emits along its local +Z, so point +Z down the nozzle.
            go.transform.localRotation = Quaternion.LookRotation(
                new Vector3(localDirection.x, localDirection.y, 0f), Vector3.forward);

            var system = go.AddComponent<ParticleSystem>();
            var main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startColor = color;
            main.playOnAwake = false;
            main.maxParticles = 200;

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.06f;

            // Fade to transparent over the particle's life so the plume has
            // a soft tail rather than a hard edge.
            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var particleRenderer = system.GetComponent<ParticleSystemRenderer>();
            particleRenderer.material = material;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortingOrder = -2;

            system.Play();
            return system;
        }

        // Stops emission entirely at idle so a coasting ship leaves no
        // trail.
        static void SetExhaustRate(ParticleSystem system, float throttle01, float rateAtFull)
        {
            if (system == null) return;
            var emission = system.emission;
            emission.rateOverTime = Mathf.Clamp01(throttle01) * rateAtFull;
        }

        static SpriteRenderer BuildFlame(Transform parent, Vector3 localPos, Color color)
        {
            var go = new GameObject("Flame");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeSprite();
            sr.color = color;
            sr.sortingOrder = -1;
            sr.transform.localScale = new Vector3(0.3f, 0.5f, 1f);
            go.SetActive(false);
            return sr;
        }

        void UpdateFlames(int key, BlockVisual v)
        {
            var ship = controller.Ship;
            if (v.TypeId == BlockTypes.Thruster || v.TypeId == BlockTypes.RetroThruster)
            {
                float t = ship.Throttle(key);
                bool visible = t >= 0.02f;
                SetExhaustRate(v.Exhaust, t, ExhaustRateAtFullThrottle);
                SetExhaustRate(v.Exhaust2, t, ExhaustRateAtFullThrottle);
                if (v.Flame != null)
                {
                    v.Flame.gameObject.SetActive(visible);
                    if (visible)
                    {
                        var s = v.Flame.transform.localScale;
                        v.Flame.transform.localScale = new Vector3(s.x, 0.5f * Mathf.Max(0.05f, t), s.z);
                    }
                }
                if (v.Flame2 != null)
                {
                    v.Flame2.gameObject.SetActive(visible);
                    if (visible)
                    {
                        var s = v.Flame2.transform.localScale;
                        v.Flame2.transform.localScale = new Vector3(s.x, 0.5f * Mathf.Max(0.05f, t), s.z);
                    }
                }
            }
            else if (v.TypeId == BlockTypes.Fin)
            {
                float steer = ship.SteerThrottle(key);
                bool visible = Mathf.Abs(steer) >= 0.02f;
                SetExhaustRate(v.Exhaust, Mathf.Abs(steer), FinPuffRateAtFullSteer);
                if (v.Flame == null) return;
                v.Flame.gameObject.SetActive(visible);
                if (!visible) return;
                var pos = v.Flame.transform.localPosition;
                pos.x = Mathf.Sign(steer) * 0.3f;
                v.Flame.transform.localPosition = pos;
                var s = v.Flame.transform.localScale;
                v.Flame.transform.localScale = new Vector3(0.3f * Mathf.Max(0.05f, Mathf.Abs(steer)), s.y, s.z);
            }
        }

        void UpdateOverlay(int key, BlockVisual v, BlockGrid grid)
        {
            if (!grid.TryGet(key, out var block)) return;

            switch (Overlay)
            {
                case OverlayMode.None:
                    v.Body.color = VariantTint(v.TypeId, block.Modifiers);
                    break;

                case OverlayMode.Damage:
                    v.Body.color = Color.Lerp(Color.white, Color.black, block.DamageFraction);
                    break;

                case OverlayMode.LoadBearing:
                    v.Body.color = _articulation.Contains(key)
                        ? new Color(0.9f, 0.1f, 0.9f)
                        : Color.Lerp(ColorForType(v.TypeId), Color.black, 0.6f);
                    break;

                case OverlayMode.Stress:
                    float ratio = 0f;
                    if (Solver != null && Solver.BlockStresses.TryGetValue(key, out var stress))
                        ratio = Mathf.Max(stress.DuctileRatio, stress.BrittleRatio);
                    if (ExtraRatioSource != null) ratio = Mathf.Max(ratio, ExtraRatioSource(key));
                    v.Body.color = StressColor(ratio);
                    break;

                case OverlayMode.Buckling:
                    float buckling = ExtraRatioSource != null ? ExtraRatioSource(key) : 0f;
                    v.Body.color = BucklingColor(buckling);
                    break;
            }

            ApplyCriticalFlash(key, v);
        }

        // Pulses a block's tint toward alarm red at or above
        // FlashRatioThreshold, on top of whatever overlay is active.
        void ApplyCriticalFlash(int key, BlockVisual v)
        {
            if (FlashRatioThreshold <= 0f) return;

            float ratio = 0f;
            if (Solver != null && Solver.BlockStresses.TryGetValue(key, out var stress))
                ratio = Mathf.Max(stress.DuctileRatio, stress.BrittleRatio);
            if (ExtraRatioSource != null) ratio = Mathf.Max(ratio, ExtraRatioSource(key));
            if (ratio < FlashRatioThreshold) return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f);
            v.Body.color = Color.Lerp(v.Body.color, new Color(1f, 0.1f, 0.1f), 0.35f + 0.55f * pulse);
        }

        // For a temporarily-transformed block (nonzero BlockVariants
        // bits) blends toward white with a slow pulse.
        static Color VariantTint(byte typeId, byte modifiers)
        {
            var baseColor = ColorForType(typeId);
            if (BlockVariants.Get(modifiers) == 0) return baseColor;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
            return Color.Lerp(baseColor, Color.white, 0.35f + 0.35f * pulse);
        }

        static Color StressColor(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            return ratio < 0.5f
                ? Color.Lerp(Color.green, Color.yellow, ratio * 2f)
                : Color.Lerp(Color.yellow, Color.red, (ratio - 0.5f) * 2f);
        }

        // Blue (no buckling risk) to red (ratio >= 1, i.e. at or past the
        // critical load factor) for the Buckling overlay.
        static Color BucklingColor(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            return Color.Lerp(Color.blue, Color.red, ratio);
        }
    }
}
