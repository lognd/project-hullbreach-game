using UnityEngine;
using Hullbreach.Hud;

namespace Hullbreach.Game
{
    // Parallax dots behind the action: without a fixed reference the void gives no sense of speed or direction.
    // Put it on the camera; the stars tile forever around it.
    // frob:doc docs/reference/hullbreach-game.md#camerafollow
    public sealed class StarField : MonoBehaviour
    {
        [SerializeField] int count = 160;

        // Wider than the widest view the zoomed-out camera can show.
        [SerializeField] float span = 140f;

        [SerializeField] int seed = 7;

        Transform[] _stars;
        Vector2[] _base;
        float[] _parallax;

        void Awake()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

            var random = new System.Random(seed);
            _stars = new Transform[count];
            _base = new Vector2[count];
            _parallax = new float[count];
            for (int i = 0; i < count; i++)
            {
                float depth = (float)random.NextDouble();
                var go = new GameObject("Star");
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = -100;
                float brightness = 0.35f + 0.5f * depth;
                renderer.color = new Color(brightness, brightness, brightness * 1.1f, 1f);
                float size = 0.1f + 0.22f * depth;
                go.transform.localScale = new Vector3(size, size, 1f);
                _stars[i] = go.transform;
                _base[i] = new Vector2((float)random.NextDouble() * span, (float)random.NextDouble() * span);
                // Nearer (brighter) stars slide faster.
                _parallax[i] = 0.05f + 0.5f * depth;
            }
        }

        void LateUpdate()
        {
            Vector3 cam = transform.position;
            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i].position = new Vector3(
                    CameraRigModel.StarOffset(_base[i].x, cam.x, _parallax[i], span),
                    CameraRigModel.StarOffset(_base[i].y, cam.y, _parallax[i], span),
                    0f);
            }
        }
    }
}
