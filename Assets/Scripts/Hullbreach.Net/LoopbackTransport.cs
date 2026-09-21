using System;
using System.Collections.Generic;

namespace Hullbreach.Net
{
    /// <summary>
    /// In-memory transport hub for tests and local (same-process) play: no
    /// sockets, no threads. <see cref="CreateEndpoint"/> hands back one
    /// ITransport per logical participant (the server, each client); every
    /// endpoint can address every other by the peer id CreateEndpoint
    /// returned for it.
    ///
    /// Simulates a lossy, jittery network on purpose (see
    /// <see cref="UnreliableDropRate"/>/<see cref="DelayTicks"/>/
    /// <see cref="JitterTicks"/>): unreliable sends may be dropped, and BOTH
    /// channels may be delivered out of send order, exactly like a real
    /// transport's internal channels can. Nothing here ever drops or
    /// duplicates a reliable message; it only reorders and delays it, which
    /// is why ClientReplica buffers reliable events by sequence number
    /// rather than trusting arrival order.
    ///
    /// Advance time by calling <see cref="Tick"/> once per simulation tick;
    /// nothing is delivered until enough ticks have passed.
    /// </summary>
    public sealed class LoopbackTransport
    {
        struct Pending
        {
            public int From;
            public int ReadyAtTick;
            public byte[] Data;
        }

        /// <summary>One participant's view of the hub: the ITransport a
        /// caller actually holds and calls Send/TryReceive on. Thin: all the
        /// real bookkeeping (timing, drop, delivery) lives on the owning
        /// LoopbackTransport hub.</summary>
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

        /// <summary>Base delay, in Tick() calls, before a sent message
        /// becomes deliverable. Zero means "as soon as the next Tick runs".</summary>
        public int DelayTicks;

        /// <summary>Extra random delay (0..JitterTicks, inclusive) added on
        /// top of DelayTicks per message, independently for every send: this
        /// is what lets two reliable messages sent in order arrive out of
        /// order, exercising ClientReplica's sequence buffering.</summary>
        public int JitterTicks;

        /// <summary>Probability (0..1) an unreliable send is silently
        /// dropped instead of queued. Never applied to reliable sends.</summary>
        public float UnreliableDropRate;

        public LoopbackTransport(int? seed = null)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Registers a new participant on this hub and returns its private
        /// ITransport view plus the peer id everyone else addresses it by.
        /// </summary>
        public ITransport CreateEndpoint(out int id)
        {
            id = _nextId++;
            var endpoint = new Endpoint(id, this);
            _endpoints[id] = endpoint;
            _inFlight[id] = new List<Pending>();
            return endpoint;
        }

        /// <summary>Wires two endpoints together: PeerConnected fires on
        /// both immediately, each naming the other's id.</summary>
        public void Connect(int a, int b)
        {
            _endpoints[a].RaiseConnected(b);
            _endpoints[b].RaiseConnected(a);
        }

        /// <summary>Marks `id` as gone: fires PeerDisconnected on `other` and
        /// drops any still-in-flight messages addressed to `id`.</summary>
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

        /// <summary>
        /// Advances the hub's clock by one tick and moves every message
        /// whose delay has elapsed from in-flight into its target's ready
        /// queue. Call once per simulation tick.
        /// </summary>
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
