using System;

namespace Hullbreach.Net
{
    /// <summary>
    /// The one interface a real transport (Unity Transport, Netcode for
    /// GameObjects, a raw socket) must implement to plug into
    /// ServerSimulation/ClientReplica. Everything above this line (message
    /// structs, ServerSimulation, ClientReplica) never touches sockets,
    /// threads or a specific networking package: it only calls this.
    ///
    /// CONTRACT the implementer must satisfy:
    ///  - SendReliable: delivered EXACTLY ONCE (no drops, no duplicates) to
    ///    the named peer. Relative ORDER between two SendReliable calls is
    ///    NOT required at this layer: a real transport may fan messages
    ///    across internal channels/priorities that reorder relative to each
    ///    other. Ordering for the messages that need it (BlockPlaced,
    ///    BlockDestroyed, FragmentSpawned, BlockDamaged, PowerupApplied) is
    ///    instead carried by the u32 sequence number embedded in the message
    ///    itself; ClientReplica buffers by sequence and applies strictly in
    ///    order, so an implementer never has to build an ordering guarantee
    ///    the application layer already provides.
    ///  - SendUnreliable: best-effort. May be dropped, duplicated, or arrive
    ///    out of order; callers (ShipState) are designed to tolerate all three.
    ///  - TryReceive: never blocks. Returns false when nothing is queued.
    ///    Reliable and unreliable payloads share the same receive queue; the
    ///    MessageKind byte at offset 0 is how the caller tells them apart,
    ///    not the channel they arrived on.
    ///  - PeerConnected/PeerDisconnected: fired exactly once per connect and
    ///    per disconnect, never for the same peer twice in a row without the
    ///    other event between.
    /// </summary>
    public interface ITransport
    {
        /// <summary>Sends a reliable, ordered message to `peer`. The
        /// implementer may copy `payload` synchronously or asynchronously,
        /// but must copy it before returning: the caller may reuse its
        /// buffer immediately.</summary>
        void SendReliable(int peer, ReadOnlySpan<byte> payload);

        /// <summary>Sends a best-effort, unordered message to `peer`. Same
        /// buffer-ownership rule as SendReliable.</summary>
        void SendUnreliable(int peer, ReadOnlySpan<byte> payload);

        /// <summary>
        /// Pops one queued message, if any. Writes into the caller-owned
        /// `into` buffer starting at offset 0 and sets `length` to the
        /// number of bytes written; `into` must be at least as large as the
        /// largest message the transport can deliver. False (peer=-1,
        /// length=0) when the queue is empty; never blocks.
        /// </summary>
        bool TryReceive(out int peer, byte[] into, out int length);

        /// <summary>Fired once, synchronously or from wherever the
        /// implementer pumps its network loop, the first time a peer becomes
        /// reachable.</summary>
        event Action<int> PeerConnected;

        /// <summary>Fired once when a peer becomes unreachable (explicit
        /// disconnect or a timeout the transport itself detects).</summary>
        event Action<int> PeerDisconnected;
    }
}
