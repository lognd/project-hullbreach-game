using System.Collections;
using System.Collections.Generic;
using Hullbreach.Match;
using Hullbreach.Net;
using Hullbreach.Platform;
using TMPro;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hullbreach.NetCode.Entities
{
    using World = Unity.Entities.World;

    /// <summary>
    /// Leaving a match that has no lobby session behind it (a bot match, LAN or direct): the Netcode worlds still hold the
    /// finished server simulation, so they are rebuilt before the next match or the lobby.
    /// </summary>
    public static class HullbreachMatchExit
    {
        public static bool IsLocalMatch => HullbreachLobbyService.Instance.Session == null;

        /// <summary>Disposes and recreates the client and server worlds (one frame later, outside any system update).</summary>
        public static bool Restarting { get; private set; }

        public static IEnumerator RestartWorlds()
        {
            Restarting = true;
            yield return null;
            var old = new List<World>();
            foreach (World w in World.All)
                if (w.IsClient() || w.IsServer() || w.IsThinClient()) old.Add(w);
            foreach (World w in old) w.Dispose();

            World server = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            ClientServerBootstrap.CreateClientWorld("ClientWorld");
            var tickRate = new ClientServerTickRate
            {
                SimulationTickRate = HullbreachNetCodeConstants.SimulationTickRate,
                NetworkTickRate = HullbreachNetCodeConstants.SimulationTickRate,
                MaxSimulationStepsPerFrame = 4,
                MaxSimulationStepBatchSize = 1,
            };
            tickRate.ResolveDefaults();
            server.EntityManager.CreateSingleton(tickRate, "Hullbreach NetCode Tick Rate");
            HullbreachNetCodeClient.LocalNetworkId = 0;
            Restarting = false;
        }
    }

    /// <summary>
    /// Post-match results screen (UX P0-2): listens to MatchEndFeed through <see cref="ResultsFlow"/> and shows
    /// VICTORY, DEFEAT or DRAW with the reason (core breached, forfeit, time limit), both players' stats and two choices:
    /// Rematch (focused) and Back to lobby. Built at runtime; all wording comes from <see cref="ResultsScreenModel"/>.
    /// </summary>
    public sealed class HullbreachResultsOverlay : MonoBehaviour, IResultsPresenter
    {
        public static HullbreachResultsOverlay Instance { get; private set; }

        Canvas _canvas;
        GameObject _root;
        TextMeshProUGUI _headline, _reason, _duration, _left, _right;
        Button _rematch, _lobby;
        ResultsScreenModel _model;
        ResultsFlow _flow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (Instance != null) return;
            var go = new GameObject("ResultsOverlay");
            DontDestroyOnLoad(go);
            go.AddComponent<HullbreachResultsOverlay>();
        }

        void Awake()
        {
            Instance = this;
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // A full-screen opaque backdrop: the storm tint, shrink timer and combat HUD sit under it, so none of them bleed past the panel.
            _backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            _backdrop.transform.SetParent(transform, false);
            var brt = (RectTransform)_backdrop.transform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            _backdrop.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 1f);
            _backdrop.SetActive(false);

            _root = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            _root.transform.SetParent(transform, false);
            var rt = (RectTransform)_root.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(860f, 520f);
            _root.GetComponent<Image>().color = new Color(0.03f, 0.07f, 0.12f, 1f);

            _headline = Text(rt, 60, new Vector2(0f, -22f), new Vector2(820f, 74f), TextAlignmentOptions.Center, FontStyles.Bold);
            _reason = Text(rt, 28, new Vector2(0f, -100f), new Vector2(820f, 38f), TextAlignmentOptions.Center, FontStyles.Normal);
            _duration = Text(rt, 24, new Vector2(0f, -140f), new Vector2(820f, 34f), TextAlignmentOptions.Center, FontStyles.Normal);
            _left = Text(rt, 24, new Vector2(40f, -194f), new Vector2(380f, 180f), TextAlignmentOptions.TopLeft, FontStyles.Normal);
            _right = Text(rt, 24, new Vector2(440f, -194f), new Vector2(380f, 180f), TextAlignmentOptions.TopLeft, FontStyles.Normal);
            _rematch = MakeButton(rt, "REBUILD AND REMATCH", new Vector2(40f, -420f), new Vector2(380f, 64f), new Color(1f, 0.42f, 0.1f, 1f));
            _lobby = MakeButton(rt, "BACK TO LOBBY", new Vector2(440f, -420f), new Vector2(380f, 64f), new Color(0.12f, 0.35f, 0.5f, 1f));
            _rematch.GetComponentInChildren<TextMeshProUGUI>().text = "REMATCH";
            _root.SetActive(false);
        }

        void OnEnable()
        {
            _flow = new ResultsFlow(this, Rematch, ToLobby, null, () => HullbreachNetCodeClient.LocalNetworkId);
            _flow.Attach(MatchEndFeed.Shared);
        }

        void OnDisable() { _flow?.Dispose(); _flow = null; }

        void OnDestroy() { if (Instance == this) Instance = null; }

        GameObject _backdrop;

        public bool IsShowing => _root != null && _root.activeSelf;

        public void Show(ResultsScreenModel model)
        {
            Unbind();
            _model = model;
            _rematch.onClick.AddListener(model.Rematch);
            _lobby.onClick.AddListener(model.ReturnToLobby);
            _headline.text = model.Headline;
            _headline.color = model.Headline == "VICTORY" ? new Color(0.3f, 0.9f, 1f)
                : model.Headline == "DEFEAT" ? new Color(1f, 0.35f, 0.3f)
                : new Color(1f, 0.8f, 0.3f);
            _reason.text = model.Reason;
            _duration.text = "Duration " + model.Duration + (HullbreachSparring.Requested ? "   (" + HullbreachSparring.UnrankedLabel + ")" : string.Empty);
            // The server does not count placements yet, so an all-zero row would only mislead: hide it until it is real.
            bool showPlaced = model.PlayerA.BlocksPlaced != "0" || model.PlayerB.BlocksPlaced != "0";
            _left.text = Lines(model.PlayerA, showPlaced);
            _right.text = Lines(model.PlayerB, showPlaced);
            _backdrop.SetActive(true);
            _root.SetActive(true);
            EnsureEventSystem();
            EventSystem.current.SetSelectedGameObject(_rematch.gameObject);
        }

        public void Hide()
        {
            Unbind();
            _backdrop.SetActive(false);
            _root.SetActive(false);
        }

        void Unbind()
        {
            if (_model == null) return;
            _rematch.onClick.RemoveListener(_model.Rematch);
            _lobby.onClick.RemoveListener(_model.ReturnToLobby);
            _model = null;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);
        }

        static string NameOf(ResultsPlayerRow p)
        {
            string who = p.Peer == HullbreachNetCodeClient.LocalNetworkId ? "You"
                : HullbreachSparring.Requested && p.Peer == HullbreachSparring.PeerId ? HullbreachSparring.DisplayName
                : "Opponent";
            return p.IsWinner ? who + "  (winner)" : who;
        }

        static string Lines(ResultsPlayerRow p, bool showPlaced) =>
            NameOf(p)
            + "\nDamage dealt: " + p.DamageDealt
            + "\nBlocks destroyed: " + p.BlocksDestroyed
            + "\nBlocks placed: " + p.BlocksPlaced
            + "\nBlocks deconstructed: " + p.BlocksDeconstructed
            + "\nTime alive: " + p.TimeAlive;

        void Rematch()
        {
            if (HullbreachSparring.Requested && HullbreachMatchExit.IsLocalMatch)
            {
                StartCoroutine(RematchBot());
                return;
            }
            HullbreachLobbyService lobby = HullbreachLobbyService.Instance;
            if (lobby.Session != null && lobby.Session.IsHost) _ = lobby.StartGameAsync();
            else if (HullbreachMatchExit.IsLocalMatch) StartCoroutine(LocalRematch());
            else Debug.Log("Hullbreach results: rematch requested; waiting for the lobby host to start.");
        }

        IEnumerator RematchBot()
        {
            var difficulty = HullbreachSparring.Difficulty;
            bool trial = HullbreachSparring.Trial;
            yield return HullbreachMatchExit.RestartWorlds();
            if (!HullbreachBotMatch.Start(difficulty, trial, out string error))
                Debug.LogWarning("Hullbreach results: rematch failed: " + error);
        }

        // A LAN or direct match: rebuild the worlds and go back to the lobby, where hosting again is one click.
        public void LeaveLocalToLobby()
        {
            HullbreachNetCodeClient.SendLeave();
            StartCoroutine(LocalRematch());
        }

        IEnumerator LocalRematch()
        {
            yield return HullbreachMatchExit.RestartWorlds();
            SceneManager.LoadScene(HullbreachLobbyService.DefaultLobbyScene);
        }

        void ToLobby()
        {
            HullbreachNetCodeClient.SendLeave(); // results-screen exit is a deliberate leave
            if (HullbreachMatchExit.IsLocalMatch) StartCoroutine(LocalRematch());
            else _ = HullbreachLobbyService.Instance.LeaveAndReturnToLobbyAsync();
        }

        static TextMeshProUGUI Text(Transform parent, float size, Vector2 pos, Vector2 box, TextAlignmentOptions align, FontStyles style)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = size; t.alignment = align; t.fontStyle = style; t.raycastTarget = false; t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = box;
            return t;
        }

        static Button MakeButton(Transform parent, string text, Vector2 pos, Vector2 size, Color colour)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = colour;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = Text(go.transform, 28, Vector2.zero, size, TextAlignmentOptions.Center, FontStyles.Bold);
            t.text = text;
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.selectedColor = new Color(1f, 1f, 1f, 1f);
            button.colors = colors;
            return button;
        }
    }
}
