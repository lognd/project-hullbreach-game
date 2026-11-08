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
        }
    }
}
