using Hullbreach.Hud;
using Hullbreach.Net;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>Counts the local player's own actions for the Cadet Trial; the feel and pickup views bump these.</summary>
    public static class HullbreachTrialProbe
    {
        public static int ShotsFired, BlocksPlaced, CratesCollected;
        public static void Reset() { ShotsFired = BlocksPlaced = CratesCollected = 0; }
    }

    /// <summary>
    /// Cadet Trial (UX P0-4) checklist: while a trial match is running it shows the steps of
    /// <see cref="CadetTrialModel"/> with done marks and the current prompt, then a completion banner. Completion is
    /// remembered (PlayerPrefs) so the lobby button can say so.
    /// </summary>
    public sealed class HullbreachCadetTrialView : MonoBehaviour
    {
        const string DoneKey = "hullbreach.cadet_trial_done";

        public static bool EverCompleted
        {
            get { try { return PlayerPrefs.GetInt(DoneKey, 0) == 1; } catch { return false; } }
        }

        public static CadetTrialModel Model { get; private set; }

        Canvas _canvas;
        TextMeshProUGUI _list;
        bool _wasRunning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            var go = new GameObject("CadetTrial");
            DontDestroyOnLoad(go);
            go.AddComponent<HullbreachCadetTrialView>();
        }

        void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 31;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            panel.GetComponent<Image>().color = new Color(0.03f, 0.07f, 0.12f, 0.8f);
            panel.GetComponent<Image>().raycastTarget = false;
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1f, 0.5f);
            prt.anchoredPosition = new Vector2(-16f, 0f);
            prt.sizeDelta = new Vector2(500f, 250f);

            var go = new GameObject("List", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(prt, false);
            _list = go.GetComponent<TextMeshProUGUI>();
            _list.fontSize = 20; _list.alignment = TextAlignmentOptions.TopLeft; _list.raycastTarget = false;
            _list.enableWordWrapping = false; _list.richText = true;
            var lrt = _list.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(14f, 10f); lrt.offsetMax = new Vector2(-10f, -10f);
            _canvas.enabled = false;
        }

        void Update()
        {
            var replica = HullbreachNetCodeClient.Replica;
            bool running = HullbreachSparring.Trial && replica != null && HullbreachNetworkGameplayController.Active
                && HullbreachNetCodeClient.IsConnected && HullbreachNetworkGameplayController.MatchRunning;
            if (!running) { _canvas.enabled = false; if (_wasRunning) Model = null; _wasRunning = false; return; }
            if (!_wasRunning) { _wasRunning = true; Model = new CadetTrialModel(); HullbreachTrialProbe.Reset(); }
            _canvas.enabled = true;

            var o = new CadetObservation
            {
                ShotsFired = HullbreachTrialProbe.ShotsFired,
                BlocksPlaced = HullbreachTrialProbe.BlocksPlaced,
                CratesCollected = HullbreachTrialProbe.CratesCollected,
            };
            if (replica.Ships.TryGetValue(HullbreachNetCodeClient.LocalNetworkId, out Hullbreach.Ship.ShipBody ship))
            {
                o.Speed = math.length(ship.Velocity);
                o.HeadingRadians = ship.Rotation;
                var zone = replica.Storm; var start = replica.StormStart;
                if (zone != null && start.HasValue)
                {
                    long tick = (long)replica.SimulationTick - start.Value.StartTick;
                    var s = zone.At((int)System.Math.Max(0L, System.Math.Min(int.MaxValue, tick)));
                    o.StormActive = true;
                    o.InsideStorm = math.distance(ship.Position, s.Center) <= s.Radius;
                }
            }
            bool was = Model.Complete;
            Model.Update(o, Time.unscaledDeltaTime);
            if (!was && Model.Complete)
            {
                try { PlayerPrefs.SetInt(DoneKey, 1); PlayerPrefs.Save(); } catch { }
                HullbreachMatchHudView.Feed.Push("CADET TRIAL COMPLETE", HudColor.SteerWhite, Time.unscaledTime);
                Hullbreach.Game.FeelFx.PlayClick();
            }
            Draw();
        }

        void Draw()
        {
            var sb = new System.Text.StringBuilder("<b>CADET TRIAL</b>   ").Append(Model.Current).Append('/').Append(Model.StepCount).Append('\n');
            for (int i = 0; i < Model.StepCount; i++)
            {
                bool done = Model.IsDone(i), current = i == Model.Current;
                string mark = done ? "[x] " : current ? "[>] " : "[ ] ";
                string colour = done ? "#6FE39A" : current ? "#FFFFFF" : "#7A93A8";
                sb.Append("<color=").Append(colour).Append('>').Append(mark).Append(CadetTrialModel.StepNames[i]).Append("</color>\n");
            }
            if (Model.Complete) sb.Append("\n<color=#6FE39A><b>").Append(Model.Prompt).Append("</b></color>");
            _list.text = sb.ToString();
        }
    }
}
