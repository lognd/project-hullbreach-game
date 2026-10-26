using System;

namespace Hullbreach.Platform
{
    // Where the sign-in screen is; the view shows the form only in Editing and Busy.
    // frob:doc docs/platform-link.md#sign-in-screen
    public enum SignInState
    {
        Editing,
        Busy,
        SignedIn,
        Offline,
    }

    // Engine-free state behind the sign-in screen (S07-2) so every branch is unit-tested;
    // the uGUI view only copies these properties onto widgets and forwards clicks.
    // frob:doc docs/platform-link.md#sign-in-screen
    public sealed class SignInModel
    {
        // frob:doc docs/platform-link.md#sign-in-screen
        public const string UnreachableHint = " You can still play offline.";

        readonly PlatformClient _client;
        readonly IPlatformLog _log;
        readonly SessionTokenStore _store;

        // Bumped by every submit and by leaving Busy, so a late login answer is dropped.
        int _attempt;

        // frob:doc docs/platform-link.md#sign-in-screen
        public SignInModel(PlatformClient client, IPlatformLog log = null, SessionTokenStore store = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _log = log ?? NullPlatformLog.Instance;
            _store = store;
            // Returning to the lobby later in the run must not ask again.
            if (PlatformSession.IsSignedIn)
            {
                State = SignInState.SignedIn;
                Message = "Signed in as " + PlatformSession.Username + ".";
            }
            else if (PlatformSession.IsOffline)
            {
                State = SignInState.Offline;
                Message = "Playing offline.";
            }
            else
            {
                State = SignInState.Editing;
                Message = string.Empty;
            }
        }

        // frob:doc docs/platform-link.md#sign-in-screen
        public event Action Changed;

        // frob:doc docs/platform-link.md#sign-in-screen
        public SignInState State { get; private set; }

        // Text for the message line; never contains the password or token.
        // frob:doc docs/platform-link.md#sign-in-screen
        public string Message { get; private set; }

        // frob:doc docs/platform-link.md#sign-in-screen
        public bool IsError { get; private set; }

        // Status of the last finished attempt, or null before any; lets the view style per outcome.
        // frob:doc docs/platform-link.md#sign-in-screen
        public LoginStatus? LastStatus { get; private set; }

        // frob:doc docs/platform-link.md#sign-in-screen
        public bool CanSubmit => State == SignInState.Editing;

        // frob:doc docs/platform-link.md#sign-in-screen
        public bool IsDone => State == SignInState.SignedIn || State == SignInState.Offline;

        // One line for the account bar the lobby shows once the screen is answered.
        // frob:doc docs/platform-link.md#sign-in-screen
        public string AccountLine =>
            State == SignInState.SignedIn ? "Signed in as " + PlatformSession.Username :
            State == SignInState.Offline ? "Playing offline" : string.Empty;

        // S07-3: reuse the token remembered from a previous launch if the platform still accepts it.
        // frob:doc docs/platform-link.md#remembered-session
        public void TryRestore()
        {
            if (_store == null || State != SignInState.Editing) return;
            if (!_store.TryLoad(out string username, out string token)) return;
            int attempt = ++_attempt;
            Set(SignInState.Busy, "Restoring your session...", isError: false);
            _client.ValidateSession(token, outcome => OnRestore(attempt, username, token, outcome));
        }

        // frob:doc docs/platform-link.md#sign-in-screen
        public void Submit(string username, string password)
        {
            if (!CanSubmit)
            {
                _log.Info($"Hullbreach sign-in: ignoring submit while {State}.");
                return;
            }
            int attempt = ++_attempt;
            Set(SignInState.Busy, "Signing in...", isError: false);
            _client.Login(username, password, outcome => OnLogin(attempt, outcome));
        }

        // Allowed while Busy too, so a slow or dead platform never traps the player.
        // frob:doc docs/platform-link.md#sign-in-screen
        public void PlayOffline()
        {
            if (IsDone) return;
            _attempt++;
            PlatformSession.PlayOffline();
            _log.Info("Hullbreach sign-in: player chose to play offline.");
            Set(SignInState.Offline, "Playing offline.", isError: false);
        }

        // Back to the form from the account bar; forgets the in-memory token.
        // frob:doc docs/platform-link.md#sign-in-screen
        public void SignOut()
        {
            if (!IsDone) return;
            _attempt++;
            bool wasSignedIn = State == SignInState.SignedIn;
            PlatformSession.SignOut();
            _store?.Clear();
            _log.Info(wasSignedIn
                ? "Hullbreach sign-in: signed out; the session token was discarded."
                : "Hullbreach sign-in: leaving offline mode to sign in.");
            LastStatus = null;
            Set(SignInState.Editing, wasSignedIn ? "Signed out." : string.Empty, isError: false);
        }

        // frob:doc docs/platform-link.md#sign-in-screen
        public static string MessageFor(LoginOutcome outcome) =>
            outcome.Status == LoginStatus.Unreachable ? outcome.Message + UnreachableHint : outcome.Message;

        void OnLogin(int attempt, LoginOutcome outcome)
        {
            if (attempt != _attempt || State != SignInState.Busy)
            {
                _log.Info($"Hullbreach sign-in: dropping a late {outcome.Status} answer; the player moved on.");
                return;
            }
            LastStatus = outcome.Status;
            if (outcome.IsSuccess)
            {
                PlatformSession.SignIn(outcome.Username, outcome.Token);
                _store?.Save(outcome.Username, outcome.Token);
                Set(SignInState.SignedIn, "Signed in as " + outcome.Username + ".", isError: false);
                return;
            }
            Set(SignInState.Editing, MessageFor(outcome), isError: true);
        }

        void OnRestore(int attempt, string username, string token, SessionOutcome outcome)
        {
            if (attempt != _attempt || State != SignInState.Busy)
            {
                _log.Info($"Hullbreach sign-in: dropping a late {outcome.Status} session check; the player moved on.");
                return;
            }
            if (outcome.IsValid)
            {
                PlatformSession.SignIn(username, token);
                _log.Info($"Hullbreach sign-in: restored the remembered session for '{username}'.");
                Set(SignInState.SignedIn, "Signed in as " + username + ".", isError: false);
                return;
            }
            // Only a 401 proves the token is dead; an unreachable platform keeps it for next time.
            bool expired = outcome.Status == SessionStatus.Invalid;
            if (expired) _store.Clear();
            _log.Info($"Hullbreach sign-in: remembered session not restored ({outcome.Status}).");
            Set(SignInState.Editing, expired ? "Your saved session expired. Sign in again." : string.Empty, isError: false);
        }

        void Set(SignInState state, string message, bool isError)
        {
            State = state;
            Message = message;
            IsError = isError;
            Changed?.Invoke();
        }
    }
}
