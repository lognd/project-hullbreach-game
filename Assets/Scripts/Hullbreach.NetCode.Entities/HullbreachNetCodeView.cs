
using System.Collections.Generic;
using Hullbreach.Core;
using Hullbreach.Net;
using Unity.Mathematics;
using UnityEngine;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>
    /// GameObject presentation for the Entities client replica. MultiplayerGame includes it
    /// by default; other gameplay scenes can add it without a ghost prefab or Asset Store content.
    /// </summary>
    [AddComponentMenu("Hullbreach/NetCode for Entities Client View")]
    public sealed class HullbreachNetCodeView : MonoBehaviour
    {
        [SerializeField] Color localShipColor = new Color(0.2f, 0.85f, 1f, 1f);
        [SerializeField] Color remoteShipColor = new Color(1f, 0.45f, 0.2f, 1f);

        [Header("Block Sprites")]
        [SerializeField] Sprite turretSprite;
        [SerializeField] Sprite thursterSprite;
        [SerializeField] Sprite armorSprite;
        [SerializeField] Sprite finSprite;
        [SerializeField] Sprite blockSprite;

        [Header("Projectile")]
        [SerializeField] Sprite projectileSprite;

        readonly Dictionary<(ushort NetId, int BlockKey), GameObject> _visuals =
            new Dictionary<(ushort, int), GameObject>();

        readonly Dictionary<uint, GameObject> _projectileVisuals =
            new Dictionary<uint, GameObject>();

        void LateUpdate()
        {
            var replica = HullbreachNetCodeClient.Replica;
            if (replica == null) return;

            var live = new HashSet<(ushort, int)>();

            foreach (var shipPair in replica.Ships)
            {
                ushort netId = shipPair.Key;
                var ship = shipPair.Value;

                foreach (var block in ship.Grid.All)
                {
                    var key = (netId, block.Key);
                    live.Add(key);

                    Sprite sprite = SpriteForType(block.Value.TypeId);

                    if (!_visuals.TryGetValue(key, out GameObject visual)
                        || visual == null)
                    {
                        visual = new GameObject(
                            $"NetCode.Block[{netId}:{block.Key}]");

                        visual.transform.SetParent(transform, false);

                        visual.AddComponent<SpriteRenderer>();

                        _visuals[key] = visual;
                    }

                    var spriteRenderer = visual.GetComponent<SpriteRenderer>();

                    // Update the sprite in case the block type changes.
                    spriteRenderer.sprite = sprite;

                    Color typeColor = ColorForType(block.Value.TypeId);
                    Color teamColor =
                        netId == HullbreachNetCodeClient.LocalNetworkId
                            ? localShipColor
                            : remoteShipColor;

                    Color display = Color.Lerp(typeColor, teamColor, 0.28f);
                    display = ApplySkin(display, netId, block.Key, block.Value.TypeId);
                    display = Color.Lerp(
                        display,
                        Color.black,
                        block.Value.DamageFraction * 0.8f);

                    spriteRenderer.color = display;

                    // Keep each block's size consistent with the grid.

                    float2 local = BlockGrid.CenterOf(block.Key);
                    float2 world = ship.LocalToWorld(local);

                    // Rotation of the entire ship, converted from radians to degrees.
                    float shipAngleDegrees = ship.Rotation * Mathf.Rad2Deg;

                    // Rotation of this individual block.
                    // Facing: 0 = up, 1 = right, 2 = down, 3 = left.
                    float facingDegrees = block.Value.Modifiers * -90f;

                    // Combine ship rotation and block facing.
                    float finalAngleDegrees = shipAngleDegrees + facingDegrees;

                    visual.transform.SetPositionAndRotation(
                        new Vector3(world.x, world.y, 0f),
                        Quaternion.Euler(0f, 0f, finalAngleDegrees)
                    );

                    // Assumes each block occupies a 1x1 grid cell.
                    visual.transform.localScale = Vector3.one;
                }
            }

            var stale = new List<(ushort, int)>();

            foreach (var pair in _visuals)
            {
                if (!live.Contains(pair.Key))
                    stale.Add(pair.Key);
            }

            foreach (var key in stale)
            {
                if (_visuals[key] != null)
                    Destroy(_visuals[key]);

                _visuals.Remove(key);
            }

            UpdateProjectiles();
        }

        Sprite SpriteForType(byte typeId)
        {
            Sprite sprite = SpriteForTypeOrNull(typeId);
            return sprite != null ? sprite : FallbackSprite();
        }

        // A scene with an unwired sprite slot must still show the ship, so fall back to a plain white square.
        static Sprite s_fallback;

        static Sprite FallbackSprite()
        {
            if (s_fallback != null) return s_fallback;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            s_fallback = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return s_fallback;
        }

        Sprite SpriteForTypeOrNull(byte typeId)
        {
            return typeId switch
            {
                BlockTypes.Cannon => turretSprite,
                BlockTypes.Thruster => thursterSprite,
                BlockTypes.RetroThruster => thursterSprite,
                BlockTypes.Armor => armorSprite,
                BlockTypes.Fin => finSprite,

                // Use blocksprite as a fallback for blocks without a dedicated sprite.
                BlockTypes.Hull => blockSprite,
                BlockTypes.Core => blockSprite,

                _ => armorSprite
            };
        }

        void UpdateProjectiles()
        {
            var projectiles = HullbreachNetCodeClient.Projectiles;
            var live = new HashSet<uint>();

            if (projectiles != null)
            {
                foreach (var pair in projectiles)
                {
                    live.Add(pair.Key);

                    if (!_projectileVisuals.TryGetValue(
                        pair.Key, out GameObject visual)
                        || visual == null)
                    {
                        visual = new GameObject(
                            $"NetCode.Projectile[{pair.Key}]");

                        visual.transform.SetParent(transform, false);

                        var spriteRenderer =
                            visual.AddComponent<SpriteRenderer>();

                        spriteRenderer.color = Color.yellow;

                        _projectileVisuals[pair.Key] = visual;
                    }

                    var projectile = pair.Value;

                    float diameter = Mathf.Max(
                        0.12f, projectile.Radius * 2f);

                    visual.transform.position = new Vector3(
                        projectile.Position.x,
                        projectile.Position.y,
                        -0.1f);

                    visual.transform.localScale =
                        new Vector3(diameter, diameter, 1f);

                    var renderer =
                        visual.GetComponent<SpriteRenderer>();

                    renderer.sprite = projectileSprite != null ? projectileSprite : FallbackSprite();
                    renderer.color = Color.yellow;
                }
            }

            var stale = new List<uint>();

            foreach (var pair in _projectileVisuals)
            {
                if (!live.Contains(pair.Key))
                    stale.Add(pair.Key);
            }

            foreach (uint id in stale)
            {
                if (_projectileVisuals[id] != null)
                    Destroy(_projectileVisuals[id]);

                _projectileVisuals.Remove(id);
            }
        }

        // Tints hull-like blocks with the owner's platform skin (ShipSkins); weapons and engines keep their colours so
        // a ship stays readable. Unknown or default skins leave the colour alone.
        static Color ApplySkin(Color color, ushort netId, int blockKey, byte typeId)
        {
            if (typeId == BlockTypes.Cannon || typeId == BlockTypes.Thruster || typeId == BlockTypes.RetroThruster)
                return color;
            if (!HullbreachNetCodeClient.ShipSkinIds.TryGetValue(netId, out byte skin) || skin == 0) return color;
            ShipSkins.Look look = ShipSkins.LookOf(skin);
            Color tinted = Color.Lerp(color, new Color(look.R, look.G, look.B), look.Mix);
            if (look.Stripes && (blockKey & 1) == 0) tinted = Color.Lerp(tinted, Color.black, 0.3f);
            return tinted;
        }

        static Color ColorForType(byte typeId) => typeId switch
        {
            BlockTypes.Core =>
                new Color(0.25f, 0.85f, 1f),

            BlockTypes.Hull =>
                new Color(0.52f, 0.6f, 0.68f),

            BlockTypes.Armor =>
                new Color(0.25f, 0.3f, 0.36f),

            BlockTypes.Thruster =>
                new Color(0.92f, 0.28f, 0.12f),

            BlockTypes.Cannon =>
                new Color(0.95f, 0.78f, 0.18f),

            BlockTypes.Fin =>
                new Color(0.35f, 0.9f, 0.5f),

            BlockTypes.RetroThruster =>
                new Color(0.18f, 0.9f, 0.42f),

            _ => Color.magenta,
        };

        void OnDestroy()
        {
            foreach (GameObject visual in _visuals.Values)
            {
                if (visual != null)
                    Destroy(visual);
            }

            _visuals.Clear();

            foreach (GameObject visual in _projectileVisuals.Values)
            {
                if (visual != null)
                    Destroy(visual);
            }

            _projectileVisuals.Clear();
        }
    }
}
