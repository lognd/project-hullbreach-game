using System.Collections.Generic;

namespace Hullbreach.Net
{
    // Everything ServerSimulation emits in one Tick; see the reference page.
    // frob:doc docs/reference/hullbreach-net.md#iserveroutbox
    public interface IServerOutbox
    {
        // Queues already-written `bytes` for `peer`.
        // frob:doc docs/reference/hullbreach-net.md#iserveroutbox
        void Send(int peer, bool reliable, byte[] bytes, int length);
    }

    // One queued send: which peer, which channel, and the exact bytes.
    // frob:doc docs/reference/hullbreach-net.md#outboxentry
    public readonly struct OutboxEntry
    {
        // frob:doc docs/reference/hullbreach-net.md#outboxentry
        public readonly int Peer;

        // frob:doc docs/reference/hullbreach-net.md#outboxentry
        public readonly bool Reliable;

        // frob:doc docs/reference/hullbreach-net.md#outboxentry
        public readonly byte[] Bytes;

        // frob:doc docs/reference/hullbreach-net.md#outboxentry
        public OutboxEntry(int peer, bool reliable, byte[] bytes)
        {
            Peer = peer;
            Reliable = reliable;
            Bytes = bytes;
        }
    }

    // The default IServerOutbox; see the reference page.
    // frob:doc docs/reference/hullbreach-net.md#serveroutbox
    public sealed class ServerOutbox : IServerOutbox
    {
        readonly List<OutboxEntry> _entries = new List<OutboxEntry>();

        // Emission order, which is also sequence order for reliable sends.
        // frob:doc docs/reference/hullbreach-net.md#serveroutbox
        public IReadOnlyList<OutboxEntry> Entries => _entries;

        // frob:doc docs/reference/hullbreach-net.md#serveroutbox
        public void Send(int peer, bool reliable, byte[] bytes, int length)
        {
            var copy = new byte[length];
            System.Array.Copy(bytes, copy, length);
            _entries.Add(new OutboxEntry(peer, reliable, copy));
        }

        // Call after forwarding queued entries to the real transport.
        // frob:doc docs/reference/hullbreach-net.md#serveroutbox
        public void Clear() => _entries.Clear();
    }
}
