using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hullbreach.Hud;

namespace Hullbreach.Game
{
    // Planet-impact warning (PULL UP / BRAKE / Terrain), flight-assist indicator and the controls hint for the
    // local ship. Builds its own overlay and is created by FeelFx when it learns the player's ship.
    // frob:doc docs/design/ui-port.md#feel-layer
    public sealed class FlightHudView : MonoBehaviour
    {
        System.Func<Hullbreach.Ship.ShipBody> _body;
        System.Func<bool> _assistOn;
        TextMeshProUGUI _warn, _assist, _hint;
        Image _edge, _arrow;
        float _nextBeep;
        readonly PlanetTrapTracker _trap = new PlanetTrapTracker();
        static Sprite _arrowSprite;

        public static void Ensure(ShipController ship)
        {
            var v = FindFirstObjectByType<FlightHudView>();
            if (v == null) v = new GameObject("FlightHud").AddComponent<FlightHudView>();
            v._body = () => ship != null ? ship.Ship : null;
            v._assistOn = () => ship != null && ship.Ship != null && ship.Ship.AssistActive;
        }

        // Online: the body is the replicated local ship; assist state comes from the input (Left Shift = off).
        public static void Ensure(System.Func<Hullbreach.Ship.ShipBody> body, System.Func<bool> assistOn)
        {
            var v = FindFirstObjectByType<FlightHudView>();
            if (v == null) v = new GameObject("FlightHud").AddComponent<FlightHudView>();
            v._body = body; v._assistOn = assistOn;
        }

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
            _warn = Text("Warning", 44, FontStyles.Bold, new Vector2(0.5f, 0.72f));
            _assist = Text("Assist", 18, FontStyles.Bold, new Vector2(0.5f, 0.06f));
            _hint = Text("Hint", 18, FontStyles.Normal, new Vector2(0.5f, 0.025f));
            _hint.text = FlightWarningModel.ControlsHint;
            _hint.color = new Color(0.78f, 0.84f, 0.94f, 0.85f);
            _warn.rectTransform.sizeDelta = new Vector2(1100f, 62f);

            // Orange-red edge tint and an arrow pointing away from the planet (PlanetWarningModel decides both).
            _edge = EdgeVignette.Create(transform, "PlanetEdge");
            _edge.transform.SetAsFirstSibling();
            var ago = new GameObject("PullAwayArrow", typeof(RectTransform), typeof(Image));
            ago.transform.SetParent(transform, false);
            _arrow = ago.GetComponent<Image>();
            _arrow.sprite = ArrowSprite(); _arrow.raycastTarget = false; _arrow.enabled = false;
            var art = (RectTransform)ago.transform;
            art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.64f);
            art.pivot = new Vector2(0.5f, 0.5f);
            art.sizeDelta = new Vector2(64f, 64f);
        }

        // A white upward triangle (tip at the top), drawn once.
        static Sprite ArrowSprite()
        {
            if (_arrowSprite != null) return _arrowSprite;
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "PullAwayArrow" };
            for (int y = 0; y < N; y++)
            {
                float half = (1f - (y + 0.5f) / N) * 0.5f * N; // wide at the bottom, a point at the top
                for (int x = 0; x < N; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Abs(x + 0.5f - N * 0.5f) <= half ? 1f : 0f));
            }
            tex.Apply();
            _arrowSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _arrowSprite;
        }

        TextMeshProUGUI Text(string name, float size, FontStyles style, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = size; t.fontStyle = style; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = anchor; rt.sizeDelta = new Vector2(700f, size * 1.4f);
            return t;
        }

        void Update()
        {
            var ship = _body?.Invoke();
            // No ship to fly (the build phase, or between rounds): no flight controls hint either.
            if (ship == null)
            {
                _warn.text = _assist.text = _hint.text = string.Empty;
                _edge.color = new Color(1f, 0.35f, 0.1f, 0f); _arrow.enabled = false;
                return;
            }
            _hint.text = FlightWarningModel.ControlsHint;
            var p = ship.QueryPlanetProximity();
            if (!p.HasBody) _trap.Reset();
            bool trapAllowed = p.HasBody && _trap.Update(Time.unscaledDeltaTime, p.Altitude, p.ClosingSpeed, p.TangentialSpeed, p.NetClimbAccel);
            PlanetWarning w = p.HasBody
                ? PlanetWarningModel.Evaluate(p.Altitude, p.ClosingSpeed, p.TangentialSpeed, p.TimeToImpact, p.PredictedImpactSpeed,
                    ship.PlanetCrash.SafeLandingSpeed, p.StopDistance, p.NetClimbAccel, (int)p.Severity, p.MustBrake, p.Heating, trapAllowed)
                : new PlanetWarning(0, string.Empty, false, false, 0f, 0f);
            string msg = w.Text;
            _warn.text = msg;
            float pulse = 1f;
            if (msg.Length > 0)
            {
                int level = w.Level;
                FlightWarningModel.Colour(level, out float r, out float g, out float b);
                float hz = FlightWarningModel.BlinkHz(level);
                pulse = hz <= 0f ? 1f : 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * hz * 2f * Mathf.PI));
                _warn.color = new Color(r, g, b, pulse);
            }

            // Edge tint (breathes with the text) and the arrow away from the planet.
            _edge.color = new Color(1f, 0.35f, 0.1f, w.EdgeAlpha * pulse);
            _arrow.enabled = w.ShowArrow;
            if (w.ShowArrow)
            {
                float roll = Camera.main != null ? Camera.main.transform.eulerAngles.z : 0f;
                _arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, PlanetWarningModel.ArrowAngleDegrees(p.SurfaceNormal.x, p.SurfaceNormal.y, roll));
                _arrow.color = new Color(1f, 0.55f, 0.2f, 0.4f + 0.6f * pulse);
            }

            // A beep that quickens with the danger (silent below the danger level).
            if (w.BeepInterval > 0f && Time.unscaledTime >= _nextBeep)
            {
                _nextBeep = Time.unscaledTime + w.BeepInterval;
                FeelFx.PlayWarningBeep(w.Level >= 4 ? 1f : 0.5f);
            }
            bool on = _assistOn != null && _assistOn();
            _assist.text = FlightWarningModel.AssistText(on);
            _assist.color = on ? new Color(0.5f, 0.9f, 1f, 0.8f) : new Color(1f, 0.75f, 0.3f, 1f);
        }
    }
}
