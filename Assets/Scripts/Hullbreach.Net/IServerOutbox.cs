using System.Collections.Generic;

namespace Hullbreach.Net
{
    /// <summary>
    /// Everything ServerSimulation emits in one Tick, before it touches an
    /// ITransport. Keeping this as a plain list of (peer, reliable, bytes)
    /// tuples is what lets the transport stay a separate concern: a test can
    /// inspect the outbox directly with no transport at all, and the
    /// transport-pumping code (a MonoBehaviour, a headless loop) is the only
    /// thing that ever needs to know both ServerSimulation and ITransport
    /// exist.
    /// </summary>
    public interface IServerOutbox
    {
        /// <summary>Queues `bytes` (already Written, length = the message's
        /// own size) for delivery to `peer` on the given channel. The
        /// implementation owns copying if it needs to outlive the caller's
        /// buffer; ServerOutbox (the default implementation) copies
        /// immediately since it is meant to be drained once per tick.</summary>
        void Send(int peer, bool reliable, byte[] bytes, int length);
    }

    /// <summary>One queued send: which peer, which channel, and the exact
    /// bytes to deliver.</summary>
    public readonly struct OutboxEntry
    {
        public readonly int Peer;
        public readonly bool Reliable;
        public readonly byte[] Bytes;

        public OutboxEntry(int peer, bool reliable, byte[] bytes)
        {
            Peer = peer;
            Reliable = reliable;
            Bytes = bytes;
        }
    }

    /// <summary>
    /// The default IServerOutbox: a plain growable list a caller drains once
    /// per tick and clears. ServerSimulation.Tick fills this; whatever pumps
    /// the real ITransport (a MonoBehaviour's FixedUpdate, a headless
    /// server's loop) reads Entries and forwards each one to
    /// transport.SendReliable/SendUnreliable, then calls Clear.
    /// </summary>
    public sealed class ServerOutbox : IServerOutbox
    {
        readonly List<OutboxEntry> _entries = new List<OutboxEntry>();

        /// <summary>Every send queued since the last Clear, in emission
        /// order (which is also sequence order for the reliable ones).</summary>
        public IReadOnlyList<OutboxEntry> Entries => _entries;

        /// <inheritdoc/>
        public void Send(int peer, bool reliable, byte[] bytes, int length)
        {
            var copy = new byte[length];
            System.Array.Copy(bytes, copy, length);
            _entries.Add(new OutboxEntry(peer, reliable, copy));
        }

        /// <summary>Drops every queued entry; call after forwarding them to
        /// the real transport for this tick.</summary>
        public void Clear() => _entries.Clear();
    }
}
