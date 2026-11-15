using Unity.Mathematics;

namespace Hullbreach.Net
{
    // What a trust event was about; each is something an honest client does not send.
    // frob:doc docs/reference/hullbreach-net.md#trustlevel
    public enum TrustEventKind : byte
    {
        // A block placement or removal the placement rules refuse.
        RejectedBuild = 0,
        // A build request while the peer is not in build mode.
        BuildOutsideBuildMode = 1,
        // Input flag bits the protocol reserves.
        ReservedInputBits = 2,
    }

    // One logged change of a peer's trust, kept so the host can forward it to the platform.
    // frob:doc docs/reference/hullbreach-net.md#trustlevel
    public readonly struct TrustEvent
    {
        public readonly int Peer;
        public readonly TrustEventKind Kind;
        public readonly uint Tick;
        public readonly float Before;
        public readonly float After;

        // frob:doc docs/reference/hullbreach-net.md#trustlevel
        public TrustEvent(int peer, TrustEventKind kind, uint tick, float before, float after)
        {
            Peer = peer;
            Kind = kind;
            Tick = tick;
            Before = before;
            After = after;
        }
    }

    // A peer's standing with the server, 1 (full) down to 0. Lag is never a trust event, so honest
    // jitter keeps full forgiveness; implausible claims shrink it, and time heals it. The server
    // tightens tolerances from it and never disconnects on it.
    // frob:doc docs/reference/hullbreach-net.md#trustlevel
    public sealed class TrustLevel
    {
        // Trust lost per implausible claim: five in a row reach the floor.
        // frob:doc docs/reference/hullbreach-net.md#trustlevel
        public const float PenaltyPerEvent = 0.2f;

        // Trust regained per second without a new event: 50 s from the floor to full.
        // frob:doc docs/reference/hullbreach-net.md#trustlevel
        public const float RecoveryPerSecond = 0.02f;

        // frob:doc docs/reference/hullbreach-net.md#trustlevel
        public float Value { get; private set; } = 1f;

        // At the floor the server refuses this peer's build claims until trust climbs back.
        // frob:doc docs/reference/hullbreach-net.md#trustlevel
        public bool AtFloor => Value <= 0f;

        // Lowers trust and returns the value before the change.
        // frob:doc docs/reference/hullbreach-net.md#trustlevel
        public float Penalize()
        {
            float before = Value;
            float next = Value - PenaltyPerEvent;
            // Float steps of 0.2 never land exactly on zero; snap so five events do reach the floor.
            Value = next < 1e-4f ? 0f : next;
            return before;
        }

        // frob:doc docs/reference/hullbreach-net.md#trustlevel
        public void Recover(float seconds)
        {
            Value = math.min(1f, Value + math.max(0f, seconds) * RecoveryPerSecond);
        }
    }
}
