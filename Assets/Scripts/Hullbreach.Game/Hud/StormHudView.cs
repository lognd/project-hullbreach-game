using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Mathematics;
using Hullbreach.Hud;
using Hullbreach.World;

namespace Hullbreach.Game
{
    // uGUI storm HUD: corner radar (planets, own ship, enemy, current and next storm ring, danger side
    // tinted), storm countdown + stage line, warning-band screen tint, an arrow to an off-screen enemy,
    // and a pull arrow toward the safe zone when outside the ring. M toggles an enlarged map.
    // All maths lives in Hullbreach.Hud (MinimapModel, StormHudModel); this only draws it. The widgets are
    // created at runtime so the same component works in the demo and the online scene with no prefab.
    // Data comes from an IStormHudSource on the same object.
    // frob:doc docs/design/ui-port.md#stormhudview
    public sealed class StormHudView : MonoBehaviour
    {
        [SerializeField] Camera worldCamera;
        [SerializeField] KeyCode toggleKey = KeyCode.M;
        [SerializeField] float smallMapSize = 230f;
        [SerializeField] float bigMapSize = 720f;

        const int SmallTexture = 192;
        const int BigTexture = 480;

        IStormHudSource _source;
        Canvas _canvas;
        RectTransform _canvasRect;
        Image _tint;
        TMP_Text _countdown, _stage;
        RectTransform _mapRect;
        RawImage _map;
        Image _selfDot, _enemyDot, _enemyArrow, _pullArrow;
        TMP_Text _pullLabel, _mapHint;
        Texture2D _tex;
        byte[] _pixels;
        int _texSize;
        float _nextRaster;
        bool _big;

        static Sprite _circle, _triangle;

        public bool IsEnlarged => _big;
        public void SetEnlarged(bool value) { if (_big != value) { _big = value; ApplyMapSize(); _nextRaster = 0f; } }

        void Awake()
        {
            _source = GetComponent<IStormHudSource>();
            if (_source == null) Debug.LogError("StormHudView needs an IStormHudSource on the same object.", this);
            Build();
        }

        void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
        }

        void Update()
        {
            if (_source == null) return;
            if (Input.GetKeyDown(toggleKey)) SetEnlarged(!_big);

            bool have = _source.TryGetFrame(out StormHudFrame f);
            _canvas.enabled = have;
            if (!have) return;
            var cam = worldCamera != null ? worldCamera : Camera.main;

            StormState s = f.Storm;
            bool storm = f.HasStorm;
            float signed = storm && f.HasLocal ? s.SignedDistance(new float2(f.LocalPosition.x, f.LocalPosition.y)) : float.MaxValue;
            bool band = storm && f.HasLocal && s.InWarningBand(new float2(f.LocalPosition.x, f.LocalPosition.y));

            // Text
            _countdown.gameObject.SetActive(storm);
            _stage.gameObject.SetActive(storm);
            if (storm)
            {
                int stage = (int)s.Stage;
                _countdown.text = StormHudModel.CountdownLine(stage, s.TicksUntilShrink, s.TicksUntilNextStage, f.TickRate);
                _stage.text = StormHudModel.StageLine(stage, s.PhaseIndex);
                bool danger = f.HasLocal && signed < 0f;
                _countdown.color = danger || stage == StormHudModel.SuddenDeath ? new Color(1f, 0.35f, 0.3f)
                    : stage == StormHudModel.Shrink ? new Color(1f, 0.8f, 0.3f) : Color.white;
            }

            FeelFx.ReportStorm(storm, signed, band);

            // Screen tint
            float a = f.HasLocal ? StormHudModel.WarningAlpha(storm, signed, band, Time.unscaledTime) : 0f;
            _tint.color = new Color(0.9f, 0.05f, 0.05f, a);

            DrawMap(f, storm);
            DrawArrows(f, cam, storm, signed);
        }

        void DrawMap(StormHudFrame f, bool storm)
        {
            float pixelRadius = _texSize * 0.5f;
            float contentR = Mathf.Max(f.ArenaRadius, 1f);
            var view = MinimapView.Fit(f.ArenaCenter.x, f.ArenaCenter.y, contentR, pixelRadius);

            if (Time.unscaledTime >= _nextRaster)
            {
                _nextRaster = Time.unscaledTime + (_big ? 0.25f : 0.1f);
                if (storm)
                    MinimapRaster.Render(_pixels, _texSize, view, f.Planets,
                        f.Storm.Center.x, f.Storm.Center.y, f.Storm.Radius,
                        f.Storm.NextCenter.x, f.Storm.NextCenter.y, f.Storm.NextRadius, _big ? 2.5f : 1.5f);
                else
                    MinimapRaster.Render(_pixels, _texSize, view, f.Planets, 0, 0, 0f, 0, 0, 0f);
                MinimapRaster.FlipRows(_pixels, _texSize);
                _tex.LoadRawTextureData(_pixels);
                _tex.Apply(false);
            }

            float k = (_big ? bigMapSize : smallMapSize) / _texSize;
            PlaceDot(_selfDot, f.HasLocal, view, f.LocalPosition, k, true);
            PlaceDot(_enemyDot, f.HasEnemy, view, f.EnemyPosition, k, true);
            DrawPickups(f, view, k);
            _mapHint.gameObject.SetActive(_big);
        }

        const int MaxPickupDots = 24;
        readonly System.Collections.Generic.List<Image> _pickupDots = new System.Collections.Generic.List<Image>();

        void DrawPickups(StormHudFrame f, MinimapView view, float k)
        {
            int n = f.Pickups != null ? Mathf.Min(f.PickupCount, MaxPickupDots) : 0;
            float dot = _big ? 10f : 6f;
            for (int i = 0; i < Mathf.Max(n, _pickupDots.Count); i++)
            {
                if (i >= _pickupDots.Count)
                    _pickupDots.Add(NewImage("PickupDot" + i, _mapRect, _circle, new Color(1f, 0.85f, 0.25f)));
                var img = _pickupDots[i];
                bool show = false;
                if (i < n)
                {
                    view.ToMap(f.Pickups[i].x, f.Pickups[i].y, out float mx, out float my);
                    show = mx * mx + my * my <= view.PixelRadius * view.PixelRadius;
                    if (show)
                    {
                        img.rectTransform.sizeDelta = new Vector2(dot, dot);
                        img.rectTransform.anchoredPosition = new Vector2(mx * k, my * k);
                    }
                }
                img.gameObject.SetActive(show);
            }
            if (_selfDot != null) _selfDot.transform.SetAsLastSibling();
        }

        void PlaceDot(Image dot, bool show, MinimapView view, Vector2 world, float k, bool clamp)
        {
            dot.gameObject.SetActive(show);
            if (!show) return;
            view.ToMapClamped(world.x, world.y, 5f, out float mx, out float my);
            ((RectTransform)dot.transform).anchoredPosition = new Vector2(mx * k, my * k);
        }

        void DrawArrows(StormHudFrame f, Camera cam, bool storm, float signed)
        {
            _enemyArrow.gameObject.SetActive(false);
            _pullArrow.gameObject.SetActive(false);
            _pullLabel.gameObject.SetActive(false);
            if (cam == null || !f.HasLocal) return;

            float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            Rect r = _canvasRect.rect;
            float halfW = r.width * 0.5f, halfH = r.height * 0.5f;
            Vector3 centre = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);

            if (f.HasEnemy)
            {
                Vector3 sp = cam.WorldToScreenPoint(new Vector3(f.EnemyPosition.x, f.EnemyPosition.y, 0f)) - centre;
                EdgeArrow arrow = EdgeArrowModel.ToTarget(halfW, halfH, 60f, sp.x / scale, sp.y / scale);
                if (arrow.Visible) PlaceArrow(_enemyArrow, arrow);
            }

            if (StormHudModel.ShowPullArrow(storm, signed))
            {
                float2 pull = f.Storm.PullDirection(new float2(f.LocalPosition.x, f.LocalPosition.y));
                // Ring of arrows around the ship (screen centre), not at the screen edge: this is "go that way", not "it is over there".
                EdgeArrow arrow = EdgeArrowModel.Along(190f, 130f, 0f, pull.x, pull.y);
                if (arrow.Visible)
                {
                    PlaceArrow(_pullArrow, arrow);
                    _pullLabel.gameObject.SetActive(true);
                    ((RectTransform)_pullLabel.transform).anchoredPosition = new Vector2(arrow.X, arrow.Y) * 1.0f + new Vector2(0f, arrow.Y >= 0 ? 34f : -34f);
                }
            }
        }

        static void PlaceArrow(Image img, EdgeArrow a)
        {
            img.gameObject.SetActive(true);
            var rt = (RectTransform)img.transform;
            rt.anchoredPosition = new Vector2(a.X, a.Y);
            rt.localRotation = Quaternion.Euler(0f, 0f, a.AngleDegrees);
        }

        void ApplyMapSize()
        {
            int tex = _big ? BigTexture : SmallTexture;
            if (_tex == null || tex != _texSize)
            {
                _texSize = tex;
                if (_tex != null) Destroy(_tex);
                _tex = new Texture2D(tex, tex, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "MinimapTex" };
                _pixels = MinimapRaster.Allocate(tex);
                _map.texture = _tex;
            }
            float size = _big ? bigMapSize : smallMapSize;
            _mapRect.sizeDelta = new Vector2(size, size);
            _mapRect.anchorMin = _mapRect.anchorMax = _mapRect.pivot = _big ? new Vector2(0.5f, 0.5f) : new Vector2(1f, 1f);
            _mapRect.anchoredPosition = _big ? Vector2.zero : new Vector2(-24f, -24f);
            float dot = _big ? 18f : 12f;
            _selfDot.rectTransform.sizeDelta = new Vector2(dot, dot);
            _enemyDot.rectTransform.sizeDelta = new Vector2(dot, dot);
        }

        void Build()
        {
            EnsureSprites();
            var canvasGo = new GameObject("StormHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRect = (RectTransform)canvasGo.transform;
            var root = _canvasRect;

            _tint = NewImage("WarningTint", root, null, new Color(0.9f, 0.05f, 0.05f, 0f));
            Stretch(_tint.rectTransform);

            _countdown = NewText("Countdown", root, 48, FontStyles.Bold, TextAlignmentOptions.Center);
            Anchor(_countdown.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(700f, 62f));
            _stage = NewText("Stage", root, 30, FontStyles.Normal, TextAlignmentOptions.Center);
            Anchor(_stage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(900f, 40f));
            _stage.color = new Color(0.8f, 0.85f, 0.95f);

            var mapGo = new GameObject("Minimap", typeof(RectTransform), typeof(RawImage));
            mapGo.transform.SetParent(root, false);
            _mapRect = (RectTransform)mapGo.transform;
            _map = mapGo.GetComponent<RawImage>();
            _map.raycastTarget = false;

            _enemyDot = NewImage("EnemyDot", _mapRect, _circle, new Color(1f, 0.25f, 0.2f));
            _selfDot = NewImage("SelfDot", _mapRect, _circle, new Color(0.3f, 0.95f, 1f));
            _mapHint = NewText("MapHint", _mapRect, 18, FontStyles.Normal, TextAlignmentOptions.Center);
            Anchor(_mapHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -40f), new Vector2(1400f, 40f));
            _mapHint.text = "White ring: safe zone   Green ring: next zone   Red: storm   (M closes)";
            _mapHint.fontSize = 28;

            _enemyArrow = NewImage("EnemyArrow", root, _triangle, new Color(1f, 0.3f, 0.25f));
            _enemyArrow.rectTransform.sizeDelta = new Vector2(46f, 46f);
            _pullArrow = NewImage("StormPullArrow", root, _triangle, new Color(0.35f, 0.9f, 1f));
            _pullArrow.rectTransform.sizeDelta = new Vector2(56f, 56f);
            _pullLabel = NewText("PullLabel", root, 30, FontStyles.Bold, TextAlignmentOptions.Center);
            _pullLabel.text = "SAFE ZONE";
            _pullLabel.color = new Color(0.35f, 0.9f, 1f);
            _pullLabel.rectTransform.sizeDelta = new Vector2(360f, 44f);
            foreach (var rt in new[] { _enemyArrow.rectTransform, _pullArrow.rectTransform, _pullLabel.rectTransform })
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

            ApplyMapSize();
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static TMP_Text NewText(string name, Transform parent, float size, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            return t;
        }

        static void EnsureSprites()
        {
            if (_circle != null && _triangle != null) return;
            const int n = 64;
            var c = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ca = Mathf.Clamp01((1f - d) * n * 0.25f);
                    // Outline ring darker than the fill so dots read on any background.
                    float shade = d > 0.72f ? 0.2f : 1f;
                    c.SetPixel(x, y, new Color(shade, shade, shade, ca));
                    // Triangle pointing +X: apex at x=1, base at x=-0.8, half-height shrinks to the apex.
                    float hx = (dx + 0.8f) / 1.8f; // 0 at base, 1 at apex
                    float edge = (1f - hx) * 0.9f - Mathf.Abs(dy);
                    float ta = hx >= 0f && hx <= 1f ? Mathf.Clamp01(edge * n * 0.25f) : 0f;
                    float ts = edge < 0.12f ? 0.35f : 1f;
                    t.SetPixel(x, y, new Color(ts, ts, ts, ta));
                }
            }
            c.Apply(); t.Apply();
            _circle = Sprite.Create(c, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            _triangle = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            _circle.hideFlags = _triangle.hideFlags = HideFlags.HideAndDontSave;
        }
    }
}
