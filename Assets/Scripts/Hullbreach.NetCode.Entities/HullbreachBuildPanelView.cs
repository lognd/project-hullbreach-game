using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Hud;
using Hullbreach.Net;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>
    /// Online build panel: block hotbar with stock or cost, budget / size / cooldown bars, a refusal line,
    /// and the READY button that ends the build phase. Draws what <see cref="BuildPanelModel"/> says and reads the
    /// replica's economy; it never decides anything. Created once at start-up, idle outside an online match.
    /// </summary>
    public sealed class HullbreachBuildPanelView : MonoBehaviour
    {
        static readonly Color Cyan = new Color(0.31f, 0.76f, 0.9f, 1f);
        static readonly Color Orange = new Color(1f, 0.42f, 0.1f, 1f);
        static readonly Color Panel = new Color(0.04f, 0.1f, 0.16f, 0.88f);

        sealed class Bar
        {
            public RectTransform Root, Fill;
            public Image FillImage;
            public TextMeshProUGUI Label;
        }

        sealed class Slot
        {
            public byte TypeId;
            public Image Background;
            public TextMeshProUGUI Title, Detail;
        }

        Canvas _canvas;
        Slot[] _slots;
        Bar _budget, _size, _cooldown;
        TextMeshProUGUI _toast, _hoverLine;
        Button _ready;
        Image _readyImage;
        TextMeshProUGUI _readyLabel;
        int _pointsLeft;
        GameObject _templateRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("BuildPanel");
            DontDestroyOnLoad(go);
            go.AddComponent<HullbreachBuildPanelView>();
        }

        void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 35;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Hotbar: one slot per buildable block (keys 1-6 pick Hull, Armor, Thruster, Cannon, Fin, Retro).
            int n = BlockTypes.Count - 1;
            _slots = new Slot[n];
            const float w = 104f, gap = 6f;
            float total = n * w + (n - 1) * gap;
            for (int i = 0; i < n; i++)
            {
                byte type = (byte)(i + 1);
                var slot = new Slot { TypeId = type };
                var go = Box("Slot" + type, transform, Panel);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
                rt.sizeDelta = new Vector2(w, 52f);
                rt.anchoredPosition = new Vector2(-total / 2f + w / 2f + i * (w + gap), 12f);
                slot.Background = go.GetComponent<Image>();
                var button = go.AddComponent<Button>();
                button.targetGraphic = slot.Background;
                button.onClick.AddListener(() => HullbreachNetworkGameplayController.SelectType(type));
                slot.Title = Label("Title", rt, 18, FontStyles.Bold, new Vector2(0f, 0.5f), new Vector2(1f, 1f), TextAlignmentOptions.Center);
                slot.Detail = Label("Detail", rt, 20, FontStyles.Normal, new Vector2(0f, 0f), new Vector2(1f, 0.5f), TextAlignmentOptions.Center);
                _slots[i] = slot;
            }

            _budget = MakeBar("Budget", new Vector2(-0.5f * (total) + 0f, 0f), total * 0.38f, 126f);
            _size = MakeBar("Size", Vector2.zero, total * 0.30f, 126f);
            _cooldown = MakeBar("Cooldown", Vector2.zero, total * 0.28f, 126f);
            LayoutBars(total);

            _toast = Label("Toast", transform, 24, FontStyles.Bold, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), TextAlignmentOptions.Center);
            var trt = _toast.rectTransform;
            trt.pivot = new Vector2(0.5f, 0f); trt.sizeDelta = new Vector2(900f, 44f); trt.anchoredPosition = new Vector2(0f, 100f);
            _toast.color = new Color(1f, 0.55f, 0.35f, 1f);

            _hoverLine = Label("HoverReason", transform, 20, FontStyles.Normal, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), TextAlignmentOptions.Center);
            var hrt = _hoverLine.rectTransform;
            hrt.pivot = new Vector2(0.5f, 0f); hrt.sizeDelta = new Vector2(900f, 30f); hrt.anchoredPosition = new Vector2(0f, 128f + 22f);
            _hoverLine.color = new Color(1f, 0.85f, 0.5f, 1f);
            hrt.anchoredPosition = new Vector2(0f, 150f);

            var readyGo = Box("ReadyButton", transform, Orange);
            var rrt = (RectTransform)readyGo.transform;
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(1f, 0f);
            rrt.sizeDelta = new Vector2(320f, 64f);
            rrt.anchoredPosition = new Vector2(-24f, 12f);
            _readyImage = readyGo.GetComponent<Image>();
            _ready = readyGo.AddComponent<Button>();
            _ready.targetGraphic = _readyImage;
            _ready.onClick.AddListener(() => HullbreachNetworkGameplayController.RequestReadyChecked(_pointsLeft));
            _readyLabel = Label("Label", rrt, 22, FontStyles.Bold, Vector2.zero, Vector2.one, TextAlignmentOptions.Center);
            _readyLabel.color = new Color(0.08f, 0.1f, 0.14f, 1f);

            // Starter ships: one click swaps the whole ship (before READY). Stacked at the lower left, clear of the hotbar.
            _templateRoot = new GameObject("Templates", typeof(RectTransform));
            _templateRoot.transform.SetParent(transform, false);
            var trootRt = (RectTransform)_templateRoot.transform;
            trootRt.anchorMin = trootRt.anchorMax = trootRt.pivot = new Vector2(0f, 0f);
            trootRt.anchoredPosition = new Vector2(16f, 12f);
            trootRt.sizeDelta = new Vector2(230f, 160f);
            var templates = ShipTemplates.All;
            var heading = Label("Heading", trootRt, 15, FontStyles.Bold, new Vector2(0f, 1f), new Vector2(1f, 1f), TextAlignmentOptions.Left);
            heading.rectTransform.anchoredPosition = new Vector2(2f, 0f);
            heading.text = "STARTER SHIPS";
            heading.color = new Color(0.7f, 0.85f, 1f, 0.9f);
            for (int i = 0; i < templates.Count; i++)
            {
                ShipTemplate template = templates[i];
                var tb = Box("Template_" + template.Id, trootRt, Panel);
                var trt2 = (RectTransform)tb.transform;
                trt2.anchorMin = trt2.anchorMax = trt2.pivot = new Vector2(0f, 0f);
                trt2.sizeDelta = new Vector2(230f, 30f);
                trt2.anchoredPosition = new Vector2(0f, (templates.Count - 1 - i) * 34f);
                var tbtn = tb.AddComponent<Button>();
                tbtn.targetGraphic = tb.GetComponent<Image>();
                tbtn.onClick.AddListener(() => HullbreachNetworkGameplayController.ApplyTemplate(template));
                var tl = Label("Text", trt2, 17, FontStyles.Bold, Vector2.zero, Vector2.one, TextAlignmentOptions.Center);
                tl.text = $"{template.Name}  ({template.Cost(BlockCosts.Default())} pt)";
            }

            SetVisible(false);
        }

        void LayoutBars(float total)
        {
            float left = -total / 2f;
            PlaceBar(_budget, left, total * 0.38f);
            PlaceBar(_size, left + total * 0.38f + 10f, total * 0.30f - 10f);
            PlaceBar(_cooldown, left + total * 0.68f + 10f, total * 0.32f - 10f);
        }

        static void PlaceBar(Bar bar, float left, float width)
        {
            bar.Root.anchorMin = bar.Root.anchorMax = bar.Root.pivot = new Vector2(0.5f, 0f);
            bar.Root.sizeDelta = new Vector2(width, 26f);
            bar.Root.anchoredPosition = new Vector2(left + width / 2f, 70f);
        }

        Bar MakeBar(string name, Vector2 _, float width, float __)
        {
            var bar = new Bar();
            var root = Box(name, transform, new Color(0.06f, 0.12f, 0.18f, 0.9f));
            bar.Root = (RectTransform)root.transform;
            var fill = Box("Fill", bar.Root, Cyan);
            bar.Fill = (RectTransform)fill.transform;
            bar.Fill.anchorMin = Vector2.zero; bar.Fill.anchorMax = Vector2.one;
            bar.Fill.offsetMin = bar.Fill.offsetMax = Vector2.zero;
            bar.FillImage = fill.GetComponent<Image>();
            bar.Label = Label("Text", bar.Root, 20, FontStyles.Bold, Vector2.zero, Vector2.one, TextAlignmentOptions.Center);
            return bar;
        }

        static void SetBar(Bar bar, BarReadout r, Color normal, bool visible)
        {
            bar.Root.gameObject.SetActive(visible);
            if (!visible) return;
            bar.Fill.anchorMax = new Vector2(Mathf.Clamp01(r.Fill), 1f);
            bar.FillImage.color = r.Low ? Orange : normal;
            bar.Label.text = r.Label;
        }

        static GameObject Box(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        static TextMeshProUGUI Label(string name, Transform parent, float size, FontStyles style, Vector2 min, Vector2 max, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = size; t.fontStyle = style; t.alignment = align; t.raycastTarget = false;
            t.textWrappingMode = TMPro.TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
            var rt = t.rectTransform; rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
        }

        void SetVisible(bool on) { if (_canvas.enabled != on) _canvas.enabled = on; }

        void Update()
        {
            var replica = HullbreachNetCodeClient.Replica;
            bool show = replica != null && HullbreachNetworkGameplayController.Active && HullbreachNetCodeClient.IsConnected;
            if (!show) { SetVisible(false); return; }
            SetVisible(true);

            var economy = replica.Economy;
            bool build = HullbreachNetworkGameplayController.IsBuildMode;
            bool running = HullbreachNetworkGameplayController.MatchRunning;
            var config = HullbreachNetworkGameplayController.Economy;
            int pointsLeft = economy.HasSnapshot ? economy.BudgetLeft : config.Costs.Budget;
            byte selected = HullbreachNetworkGameplayController.SelectedTypeId;

            foreach (var slot in _slots) slot.Background.gameObject.SetActive(build);
            _templateRoot.SetActive(build && !running && !HullbreachNetworkGameplayController.ReadySent);
            foreach (var slot in _slots)
            {
                int cost = config.Costs.CostOf(slot.TypeId);
                bool usable = BuildPanelModel.SlotUsable(running, economy.Stock(slot.TypeId), cost, pointsLeft);
                slot.Title.text = $"{slot.TypeId} {BlockTypes.Get(slot.TypeId).Name}";
                slot.Detail.text = BuildPanelModel.SlotDetail(running, economy.Stock(slot.TypeId), cost);
                bool chosen = slot.TypeId == selected;
                slot.Background.color = chosen ? new Color(0.12f, 0.34f, 0.46f, 0.95f) : Panel;
                var c = usable ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                slot.Title.color = chosen ? Cyan : c;
                slot.Detail.color = usable ? new Color(0.8f, 0.9f, 1f, 1f) : new Color(1f, 0.5f, 0.4f, 0.7f);
            }

            SetBar(_budget, BuildPanelModel.Budget(pointsLeft, config.Costs.Budget), Cyan, build && !running);
            int blocks = economy.HasSnapshot ? economy.BlockCount : 0;
            int max = economy.HasSnapshot && economy.MaxBlocks > 0 ? economy.MaxBlocks : config.Costs.MaxBlocks;
            SetBar(_size, BuildPanelModel.Size(blocks, max), new Color(0.45f, 0.7f, 0.5f, 1f), build);
            float cd = economy.CooldownSeconds(replica.SimulationTick);
            float cooldownFill = BuildCooldownModel.Fill(cd, Hullbreach.Net.ServerSimulation.DefaultBuildCooldownSeconds);
            SetBar(_cooldown, new BarReadout(1f - cooldownFill, cd > 0f ? $"Building {cd:0.0}s" : "Build ready", cd > 0f), new Color(0.45f, 0.7f, 0.5f, 1f), build && running);

            _toast.text = HullbreachNetworkGameplayController.Toasts.Text(Time.unscaledTime);
            _hoverLine.text = build ? HullbreachNetworkGameplayController.HoverReason : string.Empty;

            ReadyPhase phase = running ? ReadyPhase.Live : HullbreachNetworkGameplayController.ReadySent ? ReadyPhase.Ready : ReadyPhase.Building;
            _pointsLeft = running ? 0 : pointsLeft;
            string label = BuildPanelModel.ReadyLabel(phase, replica.Ships.Count >= 2, _pointsLeft, HullbreachNetworkGameplayController.ConfirmingReady, out bool pressable);
            _ready.gameObject.SetActive(label.Length > 0);
            _ready.interactable = pressable;
            _readyLabel.text = label;
            _readyImage.color = pressable ? (HullbreachNetworkGameplayController.ConfirmingReady ? new Color(1f, 0.75f, 0.2f, 1f) : Orange) : new Color(0.25f, 0.3f, 0.38f, 0.95f);
            _readyLabel.color = pressable ? new Color(0.08f, 0.1f, 0.14f, 1f) : new Color(0.85f, 0.9f, 1f, 1f);
            if (pressable && Input.GetKeyDown(KeyCode.Return)) HullbreachNetworkGameplayController.RequestReadyChecked(_pointsLeft);
        }
    }
}
