using UnityEngine;
using UnityEngine.UI;

namespace Hullbreach.Game
{
    // A white-to-transparent screen-edge gradient and the full-screen Image that draws it, for red/orange edge flashes
    // (core hit, planet warning). Tint it with Image.color; the alpha of the colour is the flash strength.
    // frob:doc docs/design/ux-gx.md#core-hit-feedback
    public static class EdgeVignette
    {
        static Sprite _sprite;

        public static Sprite Sprite
        {
            get
            {
                if (_sprite != null) return _sprite;
                const int N = 64;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "EdgeVignette" };
                for (int y = 0; y < N; y++)
                {
                    for (int x = 0; x < N; x++)
                    {
                        float nx = (x + 0.5f) / N * 2f - 1f, ny = (y + 0.5f) / N * 2f - 1f;
                        float d = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny)); // 0 centre .. 1 edge
                        float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d));
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                tex.Apply();
                _sprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
                return _sprite;
            }
        }

        // A full-screen, non-interactive Image under `parent` showing the vignette, fully transparent to start.
        public static Image Create(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = Sprite; img.raycastTarget = false; img.color = new Color(1f, 0.1f, 0.05f, 0f);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return img;
        }
    }
}
