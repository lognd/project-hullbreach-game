using Hullbreach.Net;
using Hullbreach.Platform;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hullbreach.NetCode.Entities
{
    // Inspector-wired queue screen (S29-2): find a match, watch the clock, cancel.
    // MatchmakingSession does the talking; QueueScreenModel supplies the text.
    // frob:ticket 01M3DG5Y1PX311E24E62K5FAKG
    // frob:doc docs/reference/hullbreach-net.md#matchmaking
    [AddComponentMenu("Hullbreach/Online/Queue View")]
    public sealed class HullbreachQueueView : MonoBehaviour
    {
        [SerializeField] GameObject panelRoot;
        [SerializeField] Button openButton;
        [SerializeField] Button findButton;
        [SerializeField] Button cancelButton;
        [SerializeField] Button closeButton;
        [SerializeField] TMP_Text statusText;

        Button _botButton;
        MatchmakingSession _session;
        MatchmakingState _lastState = MatchmakingState.Idle;

        // Raised once when a match is found; the lobby flow connects to the handoff.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public event System.Action<MatchHandoff> Matched;

        void Awake()
        {
            var client = new MatchmakingClient(HullbreachPlatformRuntime.Config, new HullbreachUnityPlatformHttp(),
                HullbreachPlatformRuntime.Log);
            var machine = new MatchmakingMachine(() => Time.realtimeSinceStartupAsDouble, MatchmakingOptions.Defaults());
            _session = new MatchmakingSession(client, machine, HullbreachPlatformRuntime.Log);
            openButton.onClick.AddListener(Open);
            findButton.onClick.AddListener(Find);
            cancelButton.onClick.AddListener(Cancel);
            closeButton.onClick.AddListener(Close);
            panelRoot.SetActive(false);
            _botButton = MakeBotButton();
        }

        // "Fight a bot" appears after QueueScreenModel.BotOfferSeconds with no opponent; the match is unranked.
        Button MakeBotButton()
        {
            var go = new GameObject("FightBotButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(panelRoot.transform, false);
            go.GetComponent<Image>().color = new Color(0.12f, 0.35f, 0.5f, 1f);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 20f);
            rt.sizeDelta = new Vector2(420f, 52f);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(go.transform, false);
            var t = label.GetComponent<TextMeshProUGUI>();
            t.text = "No opponent yet - fight a bot (unranked)";
            t.fontSize = 22; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false; t.enableWordWrapping = false;
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(FightBot);
            go.SetActive(false);
            return button;
        }

        void FightBot()
        {
            if (QueueScreenModel.CanCancel(_session.Machine)) Cancel();
            if (!HullbreachBotMatch.Start(Hullbreach.Bots.BotDifficulty.Normal, false, out string error))
                statusText.text = FriendlyError.From(error);
        }

        // Shows the panel; the find button starts a search.
        // frob:doc docs/reference/hullbreach-net.md#matchmaking
        public void Open()
        {
            panelRoot.SetActive(true);
            Debug.Log("Hullbreach queue: screen opened.");
        }

        void Close()
        {
            if (QueueScreenModel.CanCancel(_session.Machine)) Cancel();
            panelRoot.SetActive(false);
        }

        void Find()
        {
            if (!PlatformSession.IsSignedIn)
            {
                Debug.LogWarning("Hullbreach queue: not signed in; cannot search.");
                statusText.text = "Sign in to find a match.";
                statusText.color = BrandColors.Orange;
                return;
            }
            _session.Start(PlatformSession.Token);
        }

        void Cancel() => _session.Cancel();

        void Update()
        {
            _session.Tick();
            MatchmakingMachine machine = _session.Machine;
            if (!panelRoot.activeSelf && machine.State != MatchmakingState.Queued) return;

            if (_lastState != machine.State)
            {
                Debug.Log("Hullbreach queue: " + _lastState + " -> " + machine.State + ".");
                _lastState = machine.State;
                if (machine.State == MatchmakingState.Matched) Matched?.Invoke(machine.Handoff);
            }

            statusText.text = QueueScreenModel.Status(machine);
            statusText.color = QueueScreenModel.IsAlert(machine) ? BrandColors.Orange : BrandColors.Ink;
            findButton.interactable = QueueScreenModel.CanSearch(machine);
            cancelButton.interactable = QueueScreenModel.CanCancel(machine);
            _botButton.gameObject.SetActive(QueueScreenModel.OffersBot(machine));
        }
    }
}
