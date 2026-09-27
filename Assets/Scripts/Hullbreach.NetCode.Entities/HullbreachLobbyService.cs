using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>
    /// Owns Unity Authentication, Lobby, and Relay-backed Multiplayer sessions.
    /// Creating or joining a session lets the Multiplayer package configure the
    /// existing Netcode for Entities client/server worlds automatically.
    /// </summary>
    public sealed class HullbreachLobbyService : MonoBehaviour
    {
        static HullbreachLobbyService s_Instance;

        Task _initializationTask;
        ISession _session;
        IReadOnlyList<ISessionInfo> _publicSessions = Array.Empty<ISessionInfo>();
        bool _loadingGameScene;

        public const string DefaultLobbyScene = "LobbyScene";
        public const string DefaultGameScene = "MultiplayerGame";
        const string GameSceneProperty = "hullbreach.gameScene";

        public static HullbreachLobbyService Instance
        {
            get
            {
                if (s_Instance == null)
                    CreateRuntimeObject();
                return s_Instance;
            }
        }

        public event Action Changed;

        public bool IsReady { get; private set; }
        public bool IsBusy { get; private set; }
        public string Status { get; private set; } = "Starting Unity Services...";
        public string LastError { get; private set; }
        public ISession Session => _session;
        public IReadOnlyList<ISessionInfo> PublicSessions => _publicSessions;

        static void CreateRuntimeObject()
        {
            var gameObject = new GameObject("Hullbreach Online Lobby");
            DontDestroyOnLoad(gameObject);
            s_Instance = gameObject.AddComponent<HullbreachLobbyService>();
        }

        void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        async void Start()
        {
            try
            {
                await InitializeAsync();
                await RefreshPublicSessionsAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Hullbreach online services could not start: {exception.Message}");
            }
        }

        public Task InitializeAsync()
        {
            return _initializationTask ??= InitializeServicesAsync();
        }

        async Task InitializeServicesAsync()
        {
            SetBusy(true, "Connecting to Unity Services...");
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                IsReady = true;
                LastError = null;
                Status = "Online";
            }
            catch (Exception exception)
            {
                IsReady = false;
                SetError(exception);
                _initializationTask = null;
                throw;
            }
            finally
            {
                IsBusy = false;
                NotifyChanged();
            }
        }

        public async Task RefreshPublicSessionsAsync()
        {
            await InitializeAsync();
            await RunOperationAsync("Refreshing public lobbies...", async () =>
            {
                QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(
                    new QuerySessionsOptions { Count = 50 });

                _publicSessions = results.Sessions
                    .Where(session => !session.IsLocked && session.AvailableSlots > 0)
                    .OrderByDescending(session => session.LastUpdated)
                    .ToArray();
                Status = $"Found {_publicSessions.Count} public lobby" + (_publicSessions.Count == 1 ? string.Empty : "ies");
            });
        }

        public async Task HostAsync(string lobbyName, int maxPlayers, bool isPrivate, string password = null)
        {
            ValidatePassword(password);
            string cleanName = string.IsNullOrWhiteSpace(lobbyName) ? "Hullbreach Lobby" : lobbyName.Trim();
            int cleanMaxPlayers = Mathf.Clamp(maxPlayers, 2, 100);

            await InitializeAsync();
            await RunOperationAsync("Creating lobby and Relay allocation...", async () =>
            {
                await LeaveCurrentSessionInternalAsync();

                var options = new SessionOptions
                {
                    Name = cleanName,
                    MaxPlayers = cleanMaxPlayers,
                    IsPrivate = isPrivate,
                    Password = NormalizePassword(password),
                }.WithRelayNetwork();

                AttachSession(await MultiplayerService.Instance.CreateSessionAsync(options));
                Status = $"Hosting {cleanName}";
            });
        }

        public async Task JoinByCodeAsync(string joinCode, string password = null)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
                throw new ArgumentException("Enter a lobby code.", nameof(joinCode));
            ValidatePassword(password);

            await InitializeAsync();
            await RunOperationAsync("Joining lobby by code...", async () =>
            {
                await LeaveCurrentSessionInternalAsync();
                var options = new JoinSessionOptions { Password = NormalizePassword(password) };
                AttachSession(await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode.Trim(), options));
                Status = $"Joined {_session.Name}";
            });
        }

        public async Task JoinPublicSessionAsync(string sessionId, string password = null)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("A session ID is required.", nameof(sessionId));
            ValidatePassword(password);

            await InitializeAsync();
            await RunOperationAsync("Joining public lobby...", async () =>
            {
                await LeaveCurrentSessionInternalAsync();
                var options = new JoinSessionOptions { Password = NormalizePassword(password) };
                AttachSession(await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId, options));
                Status = $"Joined {_session.Name}";
            });
        }

        public async Task LeaveAsync()
        {
            await RunOperationAsync("Leaving lobby...", async () =>
            {
                await LeaveCurrentSessionInternalAsync();
                Status = "Online";
            });
        }

        public async Task StartGameAsync(string sceneName = DefaultGameScene)
        {
            if (_session == null || !_session.IsHost)
                throw new InvalidOperationException("Only the lobby host can start the game.");
            if (string.IsNullOrWhiteSpace(sceneName))
                throw new ArgumentException("A gameplay scene is required.", nameof(sceneName));

            await RunOperationAsync("Starting game...", async () =>
            {
                IHostSession host = _session.AsHost();
                host.IsLocked = true;
                host.SetProperty(GameSceneProperty,
                    new SessionProperty(sceneName.Trim(), VisibilityPropertyOptions.Member));
                await host.SavePropertiesAsync();
                Status = "Loading game...";
                LoadGameSceneIfReady();
            });
        }

        public async Task LeaveAndReturnToLobbyAsync(string lobbySceneName = DefaultLobbyScene)
        {
            await LeaveAsync();
            if (!string.IsNullOrWhiteSpace(lobbySceneName))
                await SceneManager.LoadSceneAsync(lobbySceneName.Trim(), LoadSceneMode.Single);
        }

        async Task LeaveCurrentSessionInternalAsync()
        {
            if (_session == null)
                return;

            ISession leavingSession = _session;

            // A Relay allocation belongs to its host. Deleting the host's lobby avoids
            // leaving a migrated lobby that points at a Relay allocation which no longer exists.
            if (leavingSession.IsHost)
                await leavingSession.AsHost().DeleteAsync();
            else
                await leavingSession.LeaveAsync();

            if (_session == leavingSession)
                DetachSession();
        }

        void AttachSession(ISession session)
        {
            DetachSession();
            _session = session;
            if (_session == null)
                return;

            _session.Changed += OnSessionChanged;
            _session.Deleted += OnSessionEnded;
            _session.RemovedFromSession += OnSessionEnded;
            NotifyChanged();
        }

        void DetachSession()
        {
            if (_session != null)
            {
                _session.Changed -= OnSessionChanged;
                _session.Deleted -= OnSessionEnded;
                _session.RemovedFromSession -= OnSessionEnded;
            }
            _session = null;
            _loadingGameScene = false;
            NotifyChanged();
        }

        void OnSessionChanged()
        {
            NotifyChanged();
            LoadGameSceneIfReady();
        }

        async void LoadGameSceneIfReady()
        {
            if (_loadingGameScene || _session == null ||
                !_session.Properties.TryGetValue(GameSceneProperty, out SessionProperty property) ||
                string.IsNullOrWhiteSpace(property?.Value) ||
                SceneManager.GetActiveScene().name == property.Value)
                return;

            _loadingGameScene = true;
            try
            {
                await SceneManager.LoadSceneAsync(property.Value, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                _loadingGameScene = false;
                SetError(exception);
                Debug.LogException(exception);
            }
        }

        void OnSessionEnded()
        {
            DetachSession();
            Status = "Session ended";
            NotifyChanged();
        }

        async Task RunOperationAsync(string busyStatus, Func<Task> operation)
        {
            if (IsBusy)
                throw new InvalidOperationException("Another online operation is already in progress.");

            SetBusy(true, busyStatus);
            try
            {
                await operation();
                LastError = null;
            }
            catch (Exception exception)
            {
                SetError(exception);
                throw;
            }
            finally
            {
                IsBusy = false;
                NotifyChanged();
            }
        }

        void SetBusy(bool busy, string status)
        {
            IsBusy = busy;
            Status = status;
            LastError = null;
            NotifyChanged();
        }

        void SetError(Exception exception)
        {
            LastError = exception.Message;
            Status = "Online service error";
            NotifyChanged();
        }

        static void ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return;
            if (password.Length < 8 || password.Length > 64)
                throw new ArgumentException("Lobby passwords must be 8 to 64 characters.", nameof(password));
        }

        static string NormalizePassword(string password)
        {
            return string.IsNullOrEmpty(password) ? null : password;
        }

        void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
