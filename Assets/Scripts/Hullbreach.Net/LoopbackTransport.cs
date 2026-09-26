using System;
using System.Collections.Generic;

namespace Hullbreach.Net
{
    // In-memory transport hub for tests and local (same-process) play: no
    // sockets, no threads. Deliberately simulates a lossy, jittery network
    // (see UnreliableDropRate/DelayTicks/JitterTicks): unreliable sends may
    // be dropped, and both channels may be delivered out of send order, like
    // a real transport's internal channels can. Reliable messages are never
    // dropped or duplicated here, only reordered and delayed, which is why
    // ClientReplica buffers reliable events by sequence number rather than
    // trusting arrival order.
    // frob:doc docs/reference/hullbreach-net.md#loopbacktransport
    public sealed class LoopbackTransport
    {
        struct Pending
        {
            public int From;
            public int ReadyAtTick;
            public byte[] Data;
        }

        // One participant's view of the hub: the ITransport a caller
        // actually holds. Thin: the real bookkeeping (timing, drop,
        // delivery) lives on the owning LoopbackTransport hub.
        sealed class Endpoint : ITransport
        {
            public readonly int Id;
            readonly LoopbackTransport _hub;
            public readonly Queue<(int from, byte[] data)> Ready = new Queue<(int, byte[])>();

            public event Action<int> PeerConnected;
            public event Action<int> PeerDisconnected;

            public Endpoint(int id, LoopbackTransport hub)
            {
                Id = id;
                _hub = hub;
            }

            public void RaiseConnected(int peer) => PeerConnected?.Invoke(peer);
            public void RaiseDisconnected(int peer) => PeerDisconnected?.Invoke(peer);

            public void SendReliable(int peer, ReadOnlySpan<byte> payload) => _hub.Enqueue(Id, peer, payload, reliable: true);
            public void SendUnreliable(int peer, ReadOnlySpan<byte> payload) => _hub.Enqueue(Id, peer, payload, reliable: false);

            public bool TryReceive(out int peer, byte[] into, out int length)
            {
                if (Ready.Count > 0)
                {
                    var (from, data) = Ready.Dequeue();
                    Array.Copy(data, into, data.Length);
                    length = data.Length;
                    peer = from;
                    return true;
                }
                peer = -1;
                length = 0;
                return false;
            }
        }

        readonly Dictionary<int, Endpoint> _endpoints = new Dictionary<int, Endpoint>();

        // Per-target list of messages in flight, so Tick only has to scan
        // each target's own queue rather than every message in the hub.
        readonly Dictionary<int, List<Pending>> _inFlight = new Dictionary<int, List<Pending>>();

        int _nextId = 1;
        int _tick;
        readonly Random _rng;

        // Zero means "as soon as the next Tick runs".
        // frob:doc docs/reference/hullbreach-net.md#loopbacktransport
        public int DelayTicks;

        // Added independently per send on top of DelayTicks: this is what
        // lets two reliable messages sent in order arrive out of order,
        // exercising ClientReplica's sequence buffering.
        // frob:doc docs/reference/hullbreach-net.md#loopbacktransport
        public int JitterTicks;

        // Never applied to reliable sends.
        // frob:doc docs/reference/hullbreach-net.md#loopbacktransport
        public float UnreliableDropRate;

        // frob:doc docs/reference/hullbreach-net.md#loopbacktransport
        public LoopbackTransport(int? seed = null)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        // frob:doc docs/reference/hullbreach-net.md#loopbacktransport
        public ITransport CreateEndpoint(out int id)
        {
            id = _nextId++;
            var endpoint = new Endpoint(id, this);
            _endpoints[id] = endpoint;
            _inFlight[id] = new List<Pending>();
            return endpoint;
        }

        // PeerConnected fires on both endpoints immediately.
        // frob:doc docs/reference/hullbreach-net.md#loopbacktransport
        public void Connect(int a, int b)
        {
            _endpoints[a].RaiseConnected(b);
            _endpoints[b].RaiseConnected(a);
        }

        // Fires PeerDisconnected on `other` and drops in-flight messages to `id`.
        // frob:doc docs/reference/hullbreach-net.md#loopbacktransport
        public void Disconnect(int id, int other)
        {
            if (_endpoints.Remove(id)) _inFlight.Remove(id);
            if (_endpoints.TryGetValue(other, out var ep)) ep.RaiseDisconnected(id);
        }

        void Enqueue(int from, int to, ReadOnlySpan<byte> payload, bool reliable)
        {
            if (!reliable && _rng.NextDouble() < UnreliableDropRate) return;
            if (!_inFlight.TryGetValue(to, out var list)) return; // unknown/disconnected peer: silently dropped

            int jitter = JitterTicks > 0 ? _rng.Next(0, JitterTicks + 1) : 0;
            var data = payload.ToArray();
            list.Add(new Pending { From = from, ReadyAtTick = _tick + DelayTicks + jitter, Data = data });
        }

        // Moves every message whose delay has elapsed into its target's
        // ready queue. Call once per simulation tick.
        // frob:doc docs/reference/hullbreach-net.md#loopbacktransport
        public void Tick()
        {
            _tick++;
            foreach (var kv in _inFlight)
            {
                var list = kv.Value;
                if (list.Count == 0) continue;
                if (!_endpoints.TryGetValue(kv.Key, out var ep)) { list.Clear(); continue; }

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].ReadyAtTick <= _tick)
                    {
                        ep.Ready.Enqueue((list[i].From, list[i].Data));
                        list.RemoveAt(i);
                    }
                }
            }
        }
    }
}
