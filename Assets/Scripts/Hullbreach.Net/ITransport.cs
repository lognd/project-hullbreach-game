using System;

namespace Hullbreach.Net
{
    // The interface a real transport must implement; see the reference page.
    // frob:doc docs/reference/hullbreach-net.md#itransport
    public interface ITransport
    {
        // Exactly-once delivery, order NOT required between calls; see
        // docs/netcode.md#ordering-what-the-transport-must-and-must-not-guarantee.
        // frob:doc docs/reference/hullbreach-net.md#itransport
        void SendReliable(int peer, ReadOnlySpan<byte> payload);

        // Best-effort: may be dropped, duplicated, or reordered.
        // frob:doc docs/reference/hullbreach-net.md#itransport
        void SendUnreliable(int peer, ReadOnlySpan<byte> payload);

        // Never blocks; false (peer=-1, length=0) when the queue is empty.
        // frob:doc docs/reference/hullbreach-net.md#itransport
        bool TryReceive(out int peer, byte[] into, out int length);

        // Fired exactly once the first time a peer becomes reachable.
        // frob:doc docs/reference/hullbreach-net.md#itransport
        event Action<int> PeerConnected;

        // Fired exactly once when a peer becomes unreachable.
        // frob:doc docs/reference/hullbreach-net.md#itransport
        event Action<int> PeerDisconnected;
    }
}
