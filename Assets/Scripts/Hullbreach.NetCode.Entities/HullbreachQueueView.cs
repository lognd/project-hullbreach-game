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

        Button[] _botButtons;
        GameObject _botRow;
        bool _signInBlocked;
        MatchmakingSession _session;
        MatchmakingState _lastState = MatchmakingState.Idle;

        internal bool IsVisible => panelRoot != null && panelRoot.activeInHierarchy;

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
            MakeBotRow();
        }

        // "Fight a bot" appears after QueueScreenModel.BotOfferSeconds with no opponent, when the search failed or
        // timed out, or when the player is not signed in. Easy, Normal and Hard; the suggested one is starred.
        void MakeBotRow()
        {
            _botRow = new GameObject("BotOffer", typeof(RectTransform));
            _botRow.transform.SetParent(panelRoot.transform, false);
            var rrt = (RectTransform)_botRow.transform;
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0f);
            rrt.anchoredPosition = new Vector2(0f, 16f);
            rrt.sizeDelta = new Vector2(600f, 90f);
            var header = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            header.transform.SetParent(rrt, false);
            header.text = "No opponent yet - fight a bot (unranked)";
            header.fontSize = 22; header.alignment = TextAlignmentOptions.Center; header.raycastTarget = false; header.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            header.rectTransform.anchorMin = new Vector2(0f, 1f); header.rectTransform.anchorMax = new Vector2(1f, 1f);
            header.rectTransform.pivot = new Vector2(0.5f, 1f);
            header.rectTransform.anchoredPosition = Vector2.zero; header.rectTransform.sizeDelta = new Vector2(0f, 30f);
            _botButtons = new Button[3];
            for (int tier = 0; tier < 3; tier++)
            {
                int captured = tier;
                var go = new GameObject("Bot" + tier, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(rrt, false);
                go.GetComponent<Image>().color = new Color(0.12f, 0.35f, 0.5f, 1f);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2((tier - 1) * 200f, 0f);
                rt.sizeDelta = new Vector2(190f, 56f);
                var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.transform.SetParent(go.transform, false);
                label.fontSize = 22; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false; label.textWrappingMode = TMPro.TextWrappingModes.NoWrap; label.richText = true;
                label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                var button = go.GetComponent<Button>();
                button.onClick.AddListener(() => FightBot(captured));
                _botButtons[tier] = button;
            }
            _botRow.SetActive(false);
        }

        static readonly string[] TierNames = { "EASY", "NORMAL", "HARD" };
        static readonly Hullbreach.Bots.BotDifficulty[] Tiers =
            { Hullbreach.Bots.BotDifficulty.Easy, Hullbreach.Bots.BotDifficulty.Normal, Hullbreach.Bots.BotDifficulty.Hard };

        void RefreshBotLabels(MatchmakingMachine machine)
        {
            int suggested = HullbreachBotPrefs.DefaultTier(machine.Rating, HullbreachCadetTrialView.EverCompleted);
            for (int i = 0; i < 3; i++)
                _botButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = Hullbreach.Hud.HubMenuModel.TierLabelWithHint(i, suggested);
        }

        void FightBot(int tier)
        {
            if (QueueScreenModel.CanCancel(_session.Machine)) Cancel();
            _signInBlocked = false;
            if (!HullbreachBotMatch.Start(Tiers[tier], false, out string error))
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
            _signInBlocked = false;
            panelRoot.SetActive(false);
        }

        void Find()
        {
            if (!PlatformSession.IsSignedIn)
            {
                Debug.LogWarning("Hullbreach queue: not signed in; cannot search.");
                statusText.text = "Sign in to find a match, or fight a bot below.";
                _signInBlocked = true;
                statusText.color = BrandColors.Orange;
                return;
            }
            _signInBlocked = false;
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

            if (!(_signInBlocked && machine.State == MatchmakingState.Idle)) statusText.text = QueueScreenModel.Status(machine);
            statusText.color = QueueScreenModel.IsAlert(machine) ? BrandColors.Orange : BrandColors.Ink;
            findButton.interactable = QueueScreenModel.CanSearch(machine);
            cancelButton.interactable = QueueScreenModel.CanCancel(machine);
            bool offer = QueueScreenModel.OffersBot(machine) || (_signInBlocked && machine.State != MatchmakingState.Queued);
            if (offer) RefreshBotLabels(machine);
            _botRow.SetActive(offer);
        }
    }
}
