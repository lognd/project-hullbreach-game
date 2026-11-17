using Hullbreach.Hud;
using Hullbreach.Net;
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
    /// Loading, reconnecting and disconnected states for the match scene (UX P0-6). Rules are in
    /// <see cref="ConnectionStatusModel"/>; this reads the client world (is there a NetworkId, did a snapshot arrive)
    /// and draws a loading panel, an "unstable" banner, or a "Disconnected" panel with Back to lobby.
    /// </summary>
    public sealed class HullbreachConnectionView : MonoBehaviour
    {
        GameObject _panel, _strip;
        TextMeshProUGUI _title, _detail, _stripText;
        Button _lobby;
        float _enteredAt;
        bool _everConnected;
        float _lastTraffic;
        uint _lastTick;
        World _watched;
        EntityQuery _ackQuery;
        bool _haveQuery;
        // Redials a link that dropped after working; never after Back to lobby or a finished match (ReconnectPolicy).
        // frob:ticket 01M7FJ5KYZDQ37M8MC4CEV1DG7
        readonly ReconnectPolicy _policy = new ReconnectPolicy();

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
            if (scene.name != DirectLaunchPlan.GameScene || FindFirstObjectByType<HullbreachConnectionView>() != null) return;
            new GameObject("ConnectionStatus").AddComponent<HullbreachConnectionView>();
        }

        void Awake()
        {
            _enteredAt = Time.realtimeSinceStartup;
            _lastTraffic = _enteredAt;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(transform, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = prt.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = new Color(0.03f, 0.07f, 0.12f, 0.92f);
            _title = Text(prt, 44, new Vector2(0f, 40f), new Vector2(1000f, 60f), FontStyles.Bold);
            _detail = Text(prt, 26, new Vector2(0f, -20f), new Vector2(1000f, 40f), FontStyles.Normal);
            _lobby = MakeButton(prt, "BACK TO LOBBY", new Vector2(0f, -110f), new Vector2(360f, 60f));
            _lobby.onClick.AddListener(BackToLobby);

            _strip = new GameObject("Strip", typeof(RectTransform), typeof(Image));
            _strip.transform.SetParent(transform, false);
            var srt = (RectTransform)_strip.transform;
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 1f);
            srt.anchoredPosition = new Vector2(0f, -150f);
            srt.sizeDelta = new Vector2(680f, 44f);
            _strip.GetComponent<Image>().color = new Color(0.55f, 0.38f, 0.05f, 0.95f);
            _stripText = Text(srt, 24, Vector2.zero, new Vector2(660f, 40f), FontStyles.Bold);
            _stripText.rectTransform.anchorMin = _stripText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.SetActive(false);
            _strip.SetActive(false);
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            bool connected = HullbreachNetCodeClient.IsConnected;
            if (connected)
            {
                _everConnected = true;
                if (SnapshotAdvanced()) _lastTraffic = now;
            }
            else _lastTraffic = now;

            bool matchOver = HullbreachMatchExit.Restarting ||
                (HullbreachResultsOverlay.Instance != null && HullbreachResultsOverlay.Instance.IsShowing);
            bool relay = HullbreachLobbyService.Instance.IsRelayMatch;
            if (!relay && _policy.Update(now, connected, matchOver) == ReconnectAction.Attempt)
                HullbreachNetCodeClient.TryRequestReconnect();
            ConnectionBanner b = ConnectionStatusModel.Evaluate(true, connected, _everConnected,
                now - _enteredAt, now - _lastTraffic, matchOver, relay ? null : _policy);

            bool full = b.Phase == ConnectionPhase.Connecting || b.Phase == ConnectionPhase.Lost
                || b.Phase == ConnectionPhase.Reconnecting;
            _panel.SetActive(full);
            _strip.SetActive(b.Phase == ConnectionPhase.Unstable);
            if (full)
            {
                _title.text = b.Title;
                _title.color = b.Phase == ConnectionPhase.Lost ? new Color(1f, 0.45f, 0.35f) : Color.white;
                _detail.text = b.Detail;
                _lobby.gameObject.SetActive(b.OffersLobby);
                if (b.OffersLobby && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != _lobby.gameObject)
                    EventSystem.current.SetSelectedGameObject(_lobby.gameObject);
            }
            else if (b.Phase == ConnectionPhase.Unstable) _stripText.text = b.Title + "  -  " + b.Detail;
        }

        // Netcode stamps the newest snapshot tick it received on the connection; it moves while the server talks.
        bool SnapshotAdvanced()
        {
            World world = ClientServerBootstrap.ClientWorld;
            if (world == null || !world.IsCreated) return false;
            if (!ReferenceEquals(world, _watched))
            {
                _watched = world;
                _ackQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkSnapshotAck>());
                _haveQuery = true;
            }
            if (!_haveQuery || _ackQuery.CalculateEntityCount() != 1) return false;
            NetworkSnapshotAck ack = _ackQuery.GetSingleton<NetworkSnapshotAck>();
            uint tick = ack.LastReceivedSnapshotByLocal.IsValid ? ack.LastReceivedSnapshotByLocal.TickIndexForValidTick : 0u;
            if (tick == _lastTick) return false;
            _lastTick = tick;
            return true;
        }

        async void BackToLobby()
        {
            _policy.NoteDeliberateLeave();
            HullbreachNetCodeClient.SendLeave(); // free the ship now, not after the reconnect hold
            var lobby = HullbreachLobbyService.Instance;
            if (lobby.IsRelayMatch)
            {
                _lobby.interactable = false;
                try { await lobby.LeaveAndReturnToLobbyAsync(); }
                catch (System.Exception exception)
                {
                    _lobby.interactable = true;
                    HullbreachPlatformRuntime.Log.Warning("Could not return to the lobby: " + exception.Message);
                }
            }
            else if (HullbreachResultsOverlay.Instance != null) HullbreachResultsOverlay.Instance.LeaveLocalToLobby();
            else SceneManager.LoadScene(HullbreachLobbyService.DefaultLobbyScene);
        }

        static TextMeshProUGUI Text(Transform parent, float size, Vector2 pos, Vector2 box, FontStyles style)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.fontStyle = style;
            t.raycastTarget = false; t.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = box;
            return t;
        }

        static Button MakeButton(Transform parent, string text, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(1f, 0.42f, 0.1f, 1f);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = Text(go.transform, 28, Vector2.zero, size, FontStyles.Bold);
            t.text = text;
            return go.GetComponent<Button>();
        }
    }
}
