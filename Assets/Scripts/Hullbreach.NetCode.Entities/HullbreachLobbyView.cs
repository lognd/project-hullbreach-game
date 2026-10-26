using System;
using System.Threading.Tasks;
using Hullbreach.Net;
using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>
    /// Inspector-wired uGUI view for the pre-game lobby scene. All visual objects
    /// live in LobbyCanvas.prefab so artists can restyle them without code changes.
    /// </summary>
    [AddComponentMenu("Hullbreach/Online/Lobby View")]
    public sealed class HullbreachLobbyView : MonoBehaviour
    {
        [Header("Screens")]
        [SerializeField] GameObject browserRoot;
        [SerializeField] GameObject roomRoot;

        [Header("Shared")]
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text errorText;

        [Header("Host")]
        [SerializeField] TMP_InputField lobbyNameInput;
        [SerializeField] TMP_InputField maxPlayersInput;
        [SerializeField] Toggle privateToggle;
        [SerializeField] TMP_InputField hostPasswordInput;
        [SerializeField] Button hostButton;

        [Header("Join")]
        [SerializeField] TMP_InputField joinCodeInput;
        [SerializeField] TMP_InputField joinPasswordInput;
        [SerializeField] Button joinCodeButton;
        [SerializeField] Button refreshButton;
        [SerializeField] Transform publicLobbyList;
        [SerializeField] GameObject publicLobbyRowTemplate;

        // Optional: prefabs built before LAN join lack these, so every use is null-guarded.
        [Header("LAN (no Unity Services)")]
        [SerializeField] TMP_InputField lanAddressInput;
        [SerializeField] Button lanHostButton;
        [SerializeField] Button lanJoinButton;

        [Header("Waiting Room")]
        [SerializeField] TMP_Text roomTitleText;
        [SerializeField] TMP_Text roomDetailsText;
        [SerializeField] TMP_Text joinCodeText;
        [SerializeField] TMP_Text playersText;
        [SerializeField] Button startGameButton;
        [SerializeField] Button leaveButton;
        [SerializeField] string gameplaySceneName = HullbreachLobbyService.DefaultGameScene;

        HullbreachLobbyService _service;
        string _localError;

        void OnEnable()
        {
            // Direct/LAN and server launches skip the lobby (S47-5); do not start
            // Unity Services for a scene that is about to be replaced.
            if (HullbreachDirectLaunch.Plan.IsDirect) return;
            _service = HullbreachLobbyService.Instance;
            _service.Changed += Refresh;
            hostButton.onClick.AddListener(Host);
            joinCodeButton.onClick.AddListener(JoinCode);
            refreshButton.onClick.AddListener(RefreshPublic);
            startGameButton.onClick.AddListener(StartGame);
            leaveButton.onClick.AddListener(Leave);
            if (lanHostButton != null) lanHostButton.onClick.AddListener(HostLan);
            if (lanJoinButton != null) lanJoinButton.onClick.AddListener(JoinLan);
            Refresh();
        }

        void OnDisable()
        {
            if (_service != null) _service.Changed -= Refresh;
            hostButton.onClick.RemoveListener(Host);
            joinCodeButton.onClick.RemoveListener(JoinCode);
            refreshButton.onClick.RemoveListener(RefreshPublic);
            startGameButton.onClick.RemoveListener(StartGame);
            leaveButton.onClick.RemoveListener(Leave);
            if (lanHostButton != null) lanHostButton.onClick.RemoveListener(HostLan);
            if (lanJoinButton != null) lanJoinButton.onClick.RemoveListener(JoinLan);
            ClearPublicRows();
        }

        void Host()
        {
            int maxPlayers = int.TryParse(maxPlayersInput.text, out int parsed) ? parsed : 8;
            Run(_service.HostAsync(lobbyNameInput.text, maxPlayers, privateToggle.isOn, hostPasswordInput.text));
        }

        void JoinCode()
        {
            Run(_service.JoinByCodeAsync(joinCodeInput.text, joinPasswordInput.text));
        }

        // LAN host/join bypass Unity Services; a bad address shows as the local error.
        // frob:doc docs/netcode.md#lan-host-and-join-from-the-lobby
        void HostLan()
        {
            _localError = null;
            if (!HullbreachLanLobby.TryHost(DirectLaunchPlan.DefaultPort, out string error)) _localError = error;
            Refresh();
        }

        // frob:doc docs/netcode.md#lan-host-and-join-from-the-lobby
        void JoinLan()
        {
            _localError = null;
            string text = lanAddressInput != null ? lanAddressInput.text : null;
            if (!HullbreachLanLobby.TryJoin(text, out string error)) _localError = error;
            Refresh();
        }

        void RefreshPublic() => Run(_service.RefreshPublicSessionsAsync());
        void StartGame() => Run(_service.StartGameAsync(gameplaySceneName));
        void Leave() => Run(_service.LeaveAsync());

        void Refresh()
        {
            if (_service == null) return;

            bool inRoom = _service.Session != null;
            browserRoot.SetActive(!inRoom);
            roomRoot.SetActive(inRoom);
            statusText.text = _service.Status;

            string error = !string.IsNullOrEmpty(_service.LastError) ? _service.LastError : _localError;
            errorText.gameObject.SetActive(!string.IsNullOrEmpty(error));
            errorText.text = error ?? string.Empty;

            SetInteractable(!_service.IsBusy);
            if (inRoom) RefreshRoom();
            else RebuildPublicRows();
        }

        void RefreshRoom()
        {
            ISession session = _service.Session;
            roomTitleText.text = session.Name;
            roomDetailsText.text = $"{session.PlayerCount}/{session.MaxPlayers} players  •  " +
                                   (session.IsPrivate ? "Private" : "Public") +
                                   (session.HasPassword ? "  •  Password protected" : string.Empty);
            joinCodeText.text = session.Code ?? string.Empty;
            playersText.text = BuildPlayerList(session);
            startGameButton.gameObject.SetActive(session.IsHost);
            startGameButton.interactable = session.IsHost && !_service.IsBusy;
        }

        static string BuildPlayerList(ISession session)
        {
            var lines = new System.Text.StringBuilder();
            for (int i = 0; i < session.Players.Count; i++)
            {
                string id = session.Players[i].Id ?? "Player";
                string shortId = id.Length > 10 ? id.Substring(0, 10) : id;
                lines.Append(i + 1).Append(". ").Append(shortId);
                if (id == session.Host) lines.Append("  (Host)");
                if (i + 1 < session.Players.Count) lines.AppendLine();
            }
            return lines.ToString();
        }

        void RebuildPublicRows()
        {
            ClearPublicRows();
            foreach (ISessionInfo session in _service.PublicSessions)
            {
                GameObject row = Instantiate(publicLobbyRowTemplate, publicLobbyList);
                row.name = "Lobby - " + session.Name;
                row.SetActive(true);
                row.transform.Find("NameText").GetComponent<TMP_Text>().text = session.Name;
                row.transform.Find("DetailsText").GetComponent<TMP_Text>().text =
                    $"{session.MaxPlayers - session.AvailableSlots}/{session.MaxPlayers}" +
                    (session.HasPassword ? "  •  Password" : "  •  Open");
                string sessionId = session.Id;
                row.transform.Find("JoinButton").GetComponent<Button>().onClick.AddListener(
                    () => Run(_service.JoinPublicSessionAsync(sessionId, joinPasswordInput.text)));
            }
        }

        void ClearPublicRows()
        {
            if (publicLobbyList == null || publicLobbyRowTemplate == null) return;
            for (int i = publicLobbyList.childCount - 1; i >= 0; i--)
            {
                GameObject child = publicLobbyList.GetChild(i).gameObject;
                if (child != publicLobbyRowTemplate) Destroy(child);
            }
        }

        void SetInteractable(bool value)
        {
            hostButton.interactable = value;
            joinCodeButton.interactable = value;
            refreshButton.interactable = value;
            leaveButton.interactable = value;
            if (lanHostButton != null) lanHostButton.interactable = value;
            if (lanJoinButton != null) lanJoinButton.interactable = value;
        }

        async void Run(Task operation)
        {
            _localError = null;
            try
            {
                await operation;
            }
            catch (Exception exception)
            {
                _localError = exception.Message;
                Debug.LogWarning($"Hullbreach lobby operation failed: {exception.Message}");
                Refresh();
            }
        }
    }
}
