using System.Collections.Generic;
using Hullbreach.Core;
using Hullbreach.Hud;
using Hullbreach.Net;
using Hullbreach.Ship;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>
    /// Match-critical HUD (UX P0-1): both cores' health with a danger pulse, and a short feed of what just happened.
    /// The storm countdown and the off-screen enemy arrow are StormHudView's; the planet warning is FlightHudView's.
    /// Text is at least 20 px at 720p. Pure text and colour come from <see cref="CoreHealthModel"/> and
    /// <see cref="EventFeedModel"/>; this only reads the replica and draws.
    /// </summary>
    public sealed class HullbreachMatchHudView : MonoBehaviour
    {
        // Filled by HullbreachOnlineFeel as replica events arrive.
        public static readonly EventFeedModel Feed = new EventFeedModel(6f, 5);

        sealed class CoreBar
        {
            public RectTransform Root, Fill;
            public Image FillImage, Frame;
            public TextMeshProUGUI Label;
        }

        Canvas _canvas;
        CoreBar _own, _enemy;
        TextMeshProUGUI _feed, _banner;
        readonly List<FeedLine> _lines = new List<FeedLine>(8);
        bool _announced;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("MatchHud");
            DontDestroyOnLoad(go);
            go.AddComponent<HullbreachMatchHudView>();
        }

        void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            _own = MakeBar("OwnCore", -125f);
            _enemy = MakeBar("EnemyCore", 125f);

            _feed = MakeText("Feed", 22, TextAlignmentOptions.TopLeft);
            var frt = _feed.rectTransform;
            frt.anchorMin = frt.anchorMax = frt.pivot = new Vector2(0f, 1f);
            frt.anchoredPosition = new Vector2(24f, -104f);
            frt.sizeDelta = new Vector2(520f, 150f);
            _feed.richText = true;

            _banner = MakeText("Banner", 30, TextAlignmentOptions.Center);
            var brt = _banner.rectTransform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, -122f);
            brt.sizeDelta = new Vector2(700f, 40f);
            _canvas.enabled = false;
        }

        CoreBar MakeBar(string name, float x)
        {
            var bar = new CoreBar();
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(transform, false);
            bar.Root = (RectTransform)root.transform;
            bar.Frame = root.GetComponent<Image>();
            bar.Frame.color = new Color(0.04f, 0.1f, 0.16f, 0.85f);
            bar.Frame.raycastTarget = false;
            bar.Root.anchorMin = bar.Root.anchorMax = bar.Root.pivot = new Vector2(0.5f, 1f);
            bar.Root.sizeDelta = new Vector2(230f, 30f);
            bar.Root.anchoredPosition = new Vector2(x, -82f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(bar.Root, false);
            bar.Fill = (RectTransform)fill.transform;
            bar.Fill.anchorMin = Vector2.zero; bar.Fill.anchorMax = Vector2.one;
            bar.Fill.offsetMin = bar.Fill.offsetMax = Vector2.zero;
            bar.FillImage = fill.GetComponent<Image>();
            bar.FillImage.raycastTarget = false;

            var text = MakeText("Label", 20, TextAlignmentOptions.Center, bar.Root);
            text.fontStyle = FontStyles.Bold;
            var trt = text.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            bar.Label = text;
            return bar;
        }

        TextMeshProUGUI MakeText(string name, float size, TextAlignmentOptions align, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent != null ? parent : transform, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = size; t.alignment = align; t.raycastTarget = false; t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        static Color ToColor(HudColor c, float a = 1f) => new Color(c.R, c.G, c.B, c.A * a);

        // Health of a ship's core as 0..1, or 0 when it has none.
        public static float CoreHealth(ShipBody ship)
        {
            if (ship == null) return 0f;
            foreach (var pair in ship.Grid.All)
                if (pair.Value.TypeId == BlockTypes.Core) return 1f - Mathf.Clamp01(pair.Value.DamageFraction);
            return 0f;
        }

        void Update()
        {
            var replica = HullbreachNetCodeClient.Replica;
            bool show = replica != null && HullbreachNetworkGameplayController.Active && HullbreachNetCodeClient.IsConnected;
            if (!show) { _canvas.enabled = false; _announced = false; Feed.Clear(); return; }
            _canvas.enabled = true;

            replica.Ships.TryGetValue(HullbreachNetCodeClient.LocalNetworkId, out ShipBody mine);
            ShipBody enemy = null;
            foreach (var pair in replica.Ships)
                if (pair.Key != HullbreachNetCodeClient.LocalNetworkId) { enemy = pair.Value; break; }

            bool running = HullbreachNetworkGameplayController.MatchRunning;
            if (running && !_announced) { _announced = true; Feed.Push("MATCH STARTED  -  breach their core", HudColor.SteerWhite, Time.unscaledTime); if (HullbreachSparring.Requested) Feed.Push("vs " + HullbreachSparring.DisplayName + "  -  " + HullbreachSparring.UnrankedLabel, HudColor.SteerWhite, Time.unscaledTime); if (HullbreachSparring.Trial) Feed.Push("CADET TRIAL: W/S thrust, A/D steer, Space fire, Shift assist", HudColor.SteerWhite, Time.unscaledTime); }
            if (!running) _announced = false;

            DrawBar(_own, "YOUR", mine != null, CoreHealth(mine), new HudColor(0.3f, 0.85f, 1f));
            DrawBar(_enemy, HullbreachSparring.Requested ? "BOT" : "ENEMY", enemy != null, CoreHealth(enemy), new HudColor(1f, 0.45f, 0.35f));

            // Banner: only when your own core is in real trouble.
            float health = CoreHealth(mine);
            bool critical = mine != null && CoreHealthModel.Danger(health) == CoreDanger.Critical;
            _banner.text = critical ? "CORE CRITICAL" : string.Empty;
            _banner.color = ToColor(HudColor.WarningCriticalRed, CoreHealthModel.Pulse(health, Time.unscaledTime));

            Feed.Visible(Time.unscaledTime, _lines);
            var sb = new System.Text.StringBuilder();
            foreach (var line in _lines)
            {
                Color c = ToColor(line.Colour, line.Alpha);
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(c)).Append('>').Append(line.Text).Append("</color>\n");
            }
            _feed.text = sb.ToString();
        }

        static void DrawBar(CoreBar bar, string who, bool present, float health, HudColor owner)
        {
            bar.Root.gameObject.SetActive(present);
            if (!present) return;
            float pulse = CoreHealthModel.Pulse(health, Time.unscaledTime);
            bar.Fill.anchorMax = new Vector2(Mathf.Clamp01(health), 1f);
            var fillColour = ToColor(CoreHealthModel.Colour(health, owner), pulse * 0.9f); fillColour.r *= 0.55f; fillColour.g *= 0.55f; fillColour.b *= 0.55f;
            bar.FillImage.color = fillColour;
            bar.Label.text = CoreHealthModel.Label(who, health);
            bar.Label.color = Color.white;
        }
    }
}
