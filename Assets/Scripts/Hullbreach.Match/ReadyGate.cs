using System.Collections.Generic;
using Hullbreach.Platform;

namespace Hullbreach.Match
{
    // Waiting until both peers are ready, then Started for good (a later ready or leave cannot undo it).
    // frob:doc docs/reference/hullbreach-match.md#readygate
    public enum ReadyGateState
    {
        Waiting,
        Started,
    }

    // What a ready call did; anything but Accepted left the gate unchanged.
    // frob:doc docs/reference/hullbreach-match.md#readygate
    public enum ReadyResult
    {
        Accepted,
        UnknownPeer,
        AlreadyStarted,
    }

    // Engine-free ready-up gate (roadmap S28-3): the match may start only once exactly the required
    // number of peers are present and every one of them has readied. A peer that leaves while
    // Waiting drops out along with its ready flag, so the gate can refill with a newcomer.
    // frob:ticket 01M3DG5Y1NW8M51ENVBT7EVKQY
    // frob:doc docs/reference/hullbreach-match.md#readygate
    public sealed class ReadyGate
    {
        readonly int _required;
        readonly IPlatformLog _log;
        readonly Dictionary<int, bool> _ready = new Dictionary<int, bool>();

        // frob:doc docs/reference/hullbreach-match.md#readygate
        public ReadyGateState State { get; private set; } = ReadyGateState.Waiting;

        // How many peers are currently present (ready or not).
        // frob:doc docs/reference/hullbreach-match.md#readygate
        public int PeerCount => _ready.Count;

        // requiredPeers is 2 for a head-to-head match; log defaults to silent.
        // frob:doc docs/reference/hullbreach-match.md#readygate
        public ReadyGate(int requiredPeers = 2, IPlatformLog log = null)
        {
            _required = requiredPeers;
            _log = log ?? NullPlatformLog.Instance;
        }

        // Adds a peer, not ready; false when already present or the gate has started.
        // frob:doc docs/reference/hullbreach-match.md#readygate
        public bool AddPeer(int peer)
        {
            if (State == ReadyGateState.Started || _ready.ContainsKey(peer)) return false;
            _ready[peer] = false;
            _log.Info($"Hullbreach ready gate: peer {peer} present ({_ready.Count}/{_required}).");
            return true;
        }

        // Drops a peer and its ready flag while Waiting; false when unknown or already Started.
        // frob:doc docs/reference/hullbreach-match.md#readygate
        public bool RemovePeer(int peer)
        {
            if (State == ReadyGateState.Started || !_ready.Remove(peer)) return false;
            _log.Warning($"Hullbreach ready gate: peer {peer} left before the start ({_ready.Count}/{_required}).");
            return true;
        }

        // Sets or clears a peer's ready flag; the gate flips to Started when all required peers are ready.
        // frob:doc docs/reference/hullbreach-match.md#readygate
        public ReadyResult SetReady(int peer, bool ready)
        {
            if (State == ReadyGateState.Started) return ReadyResult.AlreadyStarted;
            if (!_ready.ContainsKey(peer))
            {
                _log.Warning($"Hullbreach ready gate: ready from unknown peer {peer} ignored.");
                return ReadyResult.UnknownPeer;
            }
            _ready[peer] = ready;
            _log.Info($"Hullbreach ready gate: peer {peer} ready={ready}.");
            if (AllReady())
            {
                State = ReadyGateState.Started;
                _log.Info("Hullbreach ready gate: all peers ready, starting.");
            }
            return ReadyResult.Accepted;
        }

        // Whether a present peer has readied; false for an unknown peer.
        // frob:doc docs/reference/hullbreach-match.md#readygate
        public bool IsReady(int peer) => _ready.TryGetValue(peer, out bool r) && r;

        bool AllReady()
        {
            if (_ready.Count != _required) return false;
            foreach (var r in _ready.Values) if (!r) return false;
            return true;
        }
    }
}
