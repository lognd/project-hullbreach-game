using System;
using System.Collections.Generic;

namespace Hullbreach.Platform
{
    // What the game server should do with one connection, and what to tell its player.
    // frob:doc docs/platform-link.md#handshake
    public readonly struct AdmissionDecision
    {
        public readonly int Peer;
        public readonly bool Admitted;

        // True only when the platform accepted the token this peer presented.
        public readonly bool Verified;

        // Client-claimed platform username (bound to a verified token when Verified), or a guest label.
        public readonly string DisplayName;

        // Platform user id from /auth/session; empty for guests.
        public readonly string UserId;

        // Why the peer was rejected or left unverified; safe to send to the player and to log.
        public readonly string Reason;

        // frob:doc docs/platform-link.md#handshake
        public AdmissionDecision(int peer, bool admitted, bool verified, string displayName, string userId, string reason)
        {
            Peer = peer;
            Admitted = admitted;
            Verified = verified;
            DisplayName = displayName ?? string.Empty;
            UserId = userId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        // frob:doc docs/platform-link.md#handshake
        public override string ToString() =>
            $"peer {Peer}: {(Admitted ? "admitted" : "rejected")} " +
            $"{(Verified ? "verified" : "unverified")} name='{DisplayName}' user_id='{UserId}'" +
            (Reason.Length > 0 ? $" ({Reason})" : string.Empty);
    }

    // Server-side admission policy, free of ECS so every branch is unit-testable.
    // Not required: admit at once as a guest, upgrade to verified if a token checks out.
    // Required: hold the peer until its hello is validated; any failure (including an
    // unreachable platform, i.e. fail closed) or a silent peer is rejected.
    // frob:doc docs/platform-link.md#handshake
    public sealed class PlatformAdmissionGate
    {
        // frob:doc docs/platform-link.md#handshake
        public const int MaxDisplayNameLength = 32;

        enum Phase
        {
            AwaitingHello,
            Validating,
            Admitted,
            Rejected,
        }

        sealed class PeerState
        {
            public Phase Phase;
            public double HelloDeadline;
            public bool HelloSeen;

            // Bumped per connection so a late HTTP callback cannot touch a reused peer id.
            public int Generation;
        }

        readonly PlatformClient _client;
        readonly bool _requireAuth;
        readonly double _helloTimeoutSeconds;
        readonly IPlatformLog _log;
        readonly Dictionary<int, PeerState> _peers = new Dictionary<int, PeerState>();
        readonly Queue<AdmissionDecision> _decisions = new Queue<AdmissionDecision>();
        int _nextGeneration;

        // frob:doc docs/platform-link.md#handshake
        public PlatformAdmissionGate(PlatformClient client, bool requireAuth, double helloTimeoutSeconds,
            IPlatformLog log = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _requireAuth = requireAuth;
            _helloTimeoutSeconds = helloTimeoutSeconds > 0 ? helloTimeoutSeconds : PlatformConfig.DefaultHelloTimeoutSeconds;
            _log = log ?? NullPlatformLog.Instance;
        }

        // frob:doc docs/platform-link.md#handshake
        public bool RequireAuth => _requireAuth;

        // frob:doc docs/platform-link.md#handshake
        public void OnConnected(int peer, double now)
        {
            var state = new PeerState
            {
                Phase = Phase.AwaitingHello,
                HelloDeadline = now + _helloTimeoutSeconds,
                Generation = ++_nextGeneration,
            };
            _peers[peer] = state;
            if (_requireAuth)
            {
                _log.Info($"Hullbreach platform: peer {peer} connected; waiting up to {_helloTimeoutSeconds}s for platform sign-in.");
                return;
            }
            Admit(peer, state, verified: false, GuestName(peer), null, string.Empty);
        }

        // Token is consumed here and never stored or logged.
        // frob:doc docs/platform-link.md#handshake
        public void OnHello(int peer, string token, string claimedUsername, double now)
        {
            if (!_peers.TryGetValue(peer, out PeerState state))
            {
                _log.Warning($"Hullbreach platform: ignoring sign-in from unknown peer {peer}.");
                return;
            }
            if (state.HelloSeen)
            {
                _log.Warning($"Hullbreach platform: ignoring repeated sign-in from peer {peer}.");
                return;
            }
            state.HelloSeen = true;
            string name = SanitizeName(claimedUsername);

            if (string.IsNullOrEmpty(token))
            {
                if (_requireAuth)
                    Reject(peer, state, "This server requires a Hullbreach account. Sign in and reconnect.");
                else
                    _log.Info($"Hullbreach platform: peer {peer} is playing offline as {GuestName(peer)}.");
                return;
            }

            if (state.Phase == Phase.AwaitingHello) state.Phase = Phase.Validating;
            int generation = state.Generation;
            _log.Info($"Hullbreach platform: validating session for peer {peer} (claims '{name}').");
            _client.ValidateSession(token, outcome => OnValidated(peer, generation, name, outcome));
        }

        // frob:doc docs/platform-link.md#handshake
        public void OnDisconnected(int peer)
        {
            if (_peers.Remove(peer))
                _log.Info($"Hullbreach platform: peer {peer} disconnected; admission state cleared.");
        }

        // Rejects required-auth peers that never sent a hello in time.
        // frob:doc docs/platform-link.md#handshake
        public void Tick(double now)
        {
            List<int> expired = null;
            foreach (var pair in _peers)
            {
                if (pair.Value.Phase == Phase.AwaitingHello && _requireAuth && now >= pair.Value.HelloDeadline)
                    (expired ??= new List<int>()).Add(pair.Key);
            }
            if (expired == null) return;
            foreach (int peer in expired)
                Reject(peer, _peers[peer], "No platform sign-in received in time.");
        }

        // frob:doc docs/platform-link.md#handshake
        public bool IsAdmitted(int peer) => _peers.TryGetValue(peer, out PeerState state) && state.Phase == Phase.Admitted;

        // Decisions are queued so the ECS system can apply them on its own update.
        // frob:doc docs/platform-link.md#handshake
        public bool TryTakeDecision(out AdmissionDecision decision)
        {
            if (_decisions.Count > 0)
            {
                decision = _decisions.Dequeue();
                return true;
            }
            decision = default;
            return false;
        }

        // frob:doc docs/platform-link.md#handshake
        public static string GuestName(int peer) => "Guest #" + peer;

        // Strips control characters and caps length: the name is shown in other players' HUDs.
        // frob:doc docs/platform-link.md#handshake
        public static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var chars = new System.Text.StringBuilder(Math.Min(name.Length, MaxDisplayNameLength));
            foreach (char c in name.Trim())
            {
                if (chars.Length >= MaxDisplayNameLength) break;
                if (!char.IsControl(c)) chars.Append(c);
            }
            return chars.ToString();
        }

        void OnValidated(int peer, int generation, string claimedName, SessionOutcome outcome)
        {
            if (!_peers.TryGetValue(peer, out PeerState state) || state.Generation != generation)
            {
                _log.Info($"Hullbreach platform: dropping session result for peer {peer}; it already left.");
                return;
            }
            if (state.Phase == Phase.Rejected) return;

            if (outcome.IsValid)
            {
                string display = claimedName.Length > 0 ? claimedName : "player " + ShortId(outcome.UserId);
                Admit(peer, state, verified: true, display, outcome.UserId, string.Empty);
                return;
            }

            string reason = outcome.Status == SessionStatus.Invalid
                ? "Your platform session is invalid or expired. Sign in again."
                : "Could not verify your platform session (" + outcome.Message + ").";
            if (_requireAuth)
            {
                Reject(peer, state, reason);
                return;
            }
            _log.Warning($"Hullbreach platform: peer {peer} stays a guest: {outcome.Status} ({outcome.Message}).");
            _decisions.Enqueue(new AdmissionDecision(peer, true, false, GuestName(peer), null, reason));
        }

        void Admit(int peer, PeerState state, bool verified, string displayName, string userId, string reason)
        {
            state.Phase = Phase.Admitted;
            var decision = new AdmissionDecision(peer, true, verified, displayName, userId, reason);
            _log.Info("Hullbreach platform: " + decision);
            _decisions.Enqueue(decision);
        }

        void Reject(int peer, PeerState state, string reason)
        {
            state.Phase = Phase.Rejected;
            var decision = new AdmissionDecision(peer, false, false, string.Empty, null, reason);
            _log.Warning("Hullbreach platform: " + decision);
            _decisions.Enqueue(decision);
        }

        static string ShortId(string userId) =>
            string.IsNullOrEmpty(userId) ? "?" : userId.Length <= 8 ? userId : userId.Substring(0, 8);
    }
}
