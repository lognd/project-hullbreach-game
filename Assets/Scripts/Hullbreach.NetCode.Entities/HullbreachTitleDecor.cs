using Hullbreach.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>
    /// Title screen dressing, built at runtime so the scene file stays untouched: a slow drifting starfield behind the
    /// menu panel and the version string in the corner. Pure decoration; nothing here takes input.
    /// </summary>
    public sealed class HullbreachTitleDecor : MonoBehaviour
    {
        const int StarCount = 90;

        RectTransform _field, _canvasRect;
        bool _scattered;
        RectTransform[] _stars;
        Image[] _images;
        float[] _speed, _phase;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Spawn(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Spawn(scene);

        static void Spawn(Scene scene)
        {
            if (scene.name != "TitleScene" || FindFirstObjectByType<HullbreachTitleDecor>() != null) return;
            new GameObject("TitleDecor").AddComponent<HullbreachTitleDecor>();
        }

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            // The stars live inside the title's own background so they draw above it and below the menu panel.
            var root = GameObject.Find("TitleRoot");
            var fieldGo = new GameObject("Starfield", typeof(RectTransform));
            fieldGo.transform.SetParent(root != null ? root.transform : transform, false);
            fieldGo.transform.SetSiblingIndex(0);
            _field = (RectTransform)fieldGo.transform;
            _field.anchorMin = _field.anchorMax = _field.pivot = new Vector2(0.5f, 0.5f);
            _canvasRect = root != null ? root.GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform : null;
            _stars = new RectTransform[StarCount];
            _images = new Image[StarCount];
            _speed = new float[StarCount];
            _phase = new float[StarCount];
            var rng = new System.Random(7);
            for (int i = 0; i < StarCount; i++)
            {
                var go = new GameObject("Star", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_field, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                float size = 1.5f + (float)rng.NextDouble() * 2.5f;
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2((float)rng.NextDouble() * 1200f, (float)rng.NextDouble() * 700f);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                _stars[i] = rt; _images[i] = img;
                _speed[i] = 4f + size * 5f;                 // bigger stars are nearer, so they drift faster
                _phase[i] = (float)rng.NextDouble() * 6.28f;
            }

            var version = new GameObject("Version", typeof(RectTransform), typeof(TextMeshProUGUI));
            version.transform.SetParent(transform, false);
            var t = version.GetComponent<TextMeshProUGUI>();
            t.text = "v" + Application.version;
            t.fontSize = 18; t.color = BrandColors.Muted; t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.BottomRight;
            var vrt = t.rectTransform;
            vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(1f, 0f);
            vrt.anchoredPosition = new Vector2(-16f, 12f); vrt.sizeDelta = new Vector2(200f, 28f);
        }

        void Update()
        {
            // The title root is oversized, so the field follows the real canvas size instead.
            Vector2 size = _canvasRect != null ? _canvasRect.rect.size : new Vector2(1280f, 720f);
            _field.sizeDelta = size;
            float h = Mathf.Max(size.y, 100f), w = Mathf.Max(size.x, 100f);
            if (!_scattered)
            {
                _scattered = true;
                var rng = new System.Random(11);
                foreach (var star in _stars) star.anchoredPosition = new Vector2((float)rng.NextDouble() * w, (float)rng.NextDouble() * h);
            }
            for (int i = 0; i < StarCount; i++)
            {
                var p = _stars[i].anchoredPosition;
                p.x -= _speed[i] * 0.6f * Time.unscaledDeltaTime;
                p.y -= _speed[i] * 0.25f * Time.unscaledDeltaTime;
                if (p.x < -4f) p.x += w + 8f;
                if (p.y < -4f) p.y += h + 8f;
                _stars[i].anchoredPosition = p;
                float twinkle = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 1.3f + _phase[i]);
                _images[i].color = new Color(0.75f, 0.88f, 1f, 0.25f + 0.55f * twinkle);
            }
        }
    }
}
