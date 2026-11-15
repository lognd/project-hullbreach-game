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
        ShipController _ship;
        TextMeshProUGUI _warn, _assist, _hint;

        public static void Ensure(ShipController ship)
        {
            var v = FindFirstObjectByType<FlightHudView>();
            if (v == null) v = new GameObject("FlightHud").AddComponent<FlightHudView>();
            v._ship = ship;
        }

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
            _warn = Text("Warning", 44, FontStyles.Bold, new Vector2(0.5f, 0.72f));
            _assist = Text("Assist", 18, FontStyles.Bold, new Vector2(0.5f, 0.06f));
            _hint = Text("Hint", 15, FontStyles.Normal, new Vector2(0.5f, 0.025f));
            _hint.text = FlightWarningModel.ControlsHint;
            _hint.color = new Color(0.75f, 0.8f, 0.9f, 0.7f);
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
            if (_ship == null || _ship.Ship == null) { _warn.text = _assist.text = string.Empty; return; }
            var p = _ship.Ship.QueryPlanetProximity();
            string msg = p.HasBody ? FlightWarningModel.Text((int)p.Severity, p.MustBrake, p.Heating, p.TimeToImpact) : string.Empty;
            _warn.text = msg;
            if (msg.Length > 0)
            {
                FlightWarningModel.Colour((int)p.Severity, out float r, out float g, out float b);
                float hz = FlightWarningModel.BlinkHz((int)p.Severity);
                float a = hz <= 0f ? 1f : 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * hz * 2f * Mathf.PI));
                _warn.color = new Color(r, g, b, a);
            }
            bool on = _ship.Ship.AssistActive;
            _assist.text = FlightWarningModel.AssistText(on);
            _assist.color = on ? new Color(0.5f, 0.9f, 1f, 0.8f) : new Color(1f, 0.75f, 0.3f, 1f);
        }
    }
}
