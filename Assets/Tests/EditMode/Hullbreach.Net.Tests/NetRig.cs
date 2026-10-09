using System;
using Hullbreach.Net;

namespace Hullbreach.Net.Tests
{
    // How hostile a LoopbackTransport link is: per-send delay and jitter in sim ticks, unreliable loss.
    // frob:doc docs/testing.md#throttled-connection-harness
    public readonly struct LinkProfile
    {
        // frob:doc docs/testing.md#throttled-connection-harness
        public readonly int DelayTicks;

        // frob:doc docs/testing.md#throttled-connection-harness
        public readonly int JitterTicks;

        // frob:doc docs/testing.md#throttled-connection-harness
        public readonly float UnreliableDropRate;

        // frob:doc docs/testing.md#throttled-connection-harness
        public LinkProfile(int delayTicks, int jitterTicks, float unreliableDropRate)
        {
            DelayTicks = delayTicks;
            JitterTicks = jitterTicks;
            UnreliableDropRate = unreliableDropRate;
        }

        // No delay, no loss: the baseline the throttled runs are compared against.
        // frob:doc docs/testing.md#throttled-connection-harness
        public static LinkProfile Ideal => new LinkProfile(0, 0, 0f);

        // Mean one-way latency is DelayTicks + JitterTicks / 2, so the round trip is 2 * Delay + Jitter ticks.
        // frob:doc docs/testing.md#throttled-connection-harness
        public static LinkProfile FromRoundTrip(float roundTripMs, float tickRate, float unreliableDropRate)
        {
            int roundTripTicks = (int)Math.Round(roundTripMs * tickRate / 1000f);
            return new LinkProfile(roundTripTicks / 2, roundTripTicks % 2, unreliableDropRate);
        }

        // The S48 target: 100 ms round trip and 2 percent loss.
        // frob:doc docs/testing.md#throttled-connection-harness
        public static LinkProfile Throttled(float tickRate = NetRig.TickRate) => FromRoundTrip(100f, tickRate, 0.02f);

        // frob:doc docs/testing.md#throttled-connection-harness
        public float RoundTripMs(float tickRate) => (2 * DelayTicks + JitterTicks) * 1000f / tickRate;

        // frob:doc docs/testing.md#throttled-connection-harness
        public void ApplyTo(LoopbackTransport hub)
        {
            hub.DelayTicks = DelayTicks;
            hub.JitterTicks = JitterTicks;
            hub.UnreliableDropRate = UnreliableDropRate;
        }
    }

    // A server host and two client replicas wired through one LoopbackTransport hub; one Step is one sim tick.
    // frob:doc docs/testing.md#throttled-connection-harness
    public sealed class NetRig
    {
        // frob:doc docs/testing.md#throttled-connection-harness
        public const float TickRate = 50f;

        // frob:doc docs/testing.md#throttled-connection-harness
        public readonly LoopbackTransport Hub;

        // frob:doc docs/testing.md#throttled-connection-harness
        public readonly ServerHost Host;

        // frob:doc docs/testing.md#throttled-connection-harness
        public readonly ITransport ServerEndpoint, ClientA, ClientB;

        // frob:doc docs/testing.md#throttled-connection-harness
        public readonly int ServerId, IdA, IdB;

        // frob:doc docs/testing.md#throttled-connection-harness
        public readonly ClientReplica ReplicaA = new ClientReplica();

        // frob:doc docs/testing.md#throttled-connection-harness
        public readonly ClientReplica ReplicaB = new ClientReplica();

        readonly bool _pumpClients;
        readonly byte[] _buffer = new byte[2048];

        // frob:doc docs/testing.md#throttled-connection-harness
        public uint ClientTick { get; private set; }

        // With pumpClients false the test reads the client endpoints itself and the replicas stay empty.
        // spareIds burns that many endpoint ids first, pushing peer ids clear of the small ids fragments get.
        // frob:doc docs/testing.md#throttled-connection-harness
        public NetRig(LinkProfile link, int seed = 1, bool pumpClients = true, int spareIds = 0)
        {
            _pumpClients = pumpClients;
            Hub = new LoopbackTransport(seed);
            link.ApplyTo(Hub);
            for (int i = 0; i < spareIds; i++) Hub.CreateEndpoint(out _);
            ServerEndpoint = Hub.CreateEndpoint(out ServerId);
            ClientA = Hub.CreateEndpoint(out IdA);
            ClientB = Hub.CreateEndpoint(out IdB);
            Hub.Connect(ServerId, IdA);
            Hub.Connect(ServerId, IdB);
            Host = new ServerHost(ServerEndpoint, TickRate);
        }

        // Order inside a tick: clients send, the host drains and simulates, the hub delivers, clients apply.
        // frob:doc docs/testing.md#throttled-connection-harness
        public void Step(Action beforeHost = null)
        {
            beforeHost?.Invoke();
            Host.Advance(1.0 / TickRate);
            Hub.Tick();
            ClientTick++;
            if (_pumpClients)
            {
                Pump(ClientA, ReplicaA);
                Pump(ClientB, ReplicaB);
            }
        }

        // frob:doc docs/testing.md#throttled-connection-harness
        public void Play(int ticks, Action<int> beforeHost = null)
        {
            for (int i = 0; i < ticks; i++)
            {
                int tick = i;
                Step(beforeHost == null ? (Action)null : () => beforeHost(tick));
            }
        }

        // frob:doc docs/testing.md#throttled-connection-harness
        public static byte[] InputBytes(ushort netId, uint tick, float thrust, float steer)
        {
            var buf = new byte[InputMessage.ByteSize];
            var w = new ByteWriter(buf);
            InputMessage.FromFloats(netId, tick, thrust, steer, false).Write(ref w);
            return buf;
        }

        void Pump(ITransport client, ClientReplica replica)
        {
            while (client.TryReceive(out _, _buffer, out int length))
                replica.ApplyReceived(_buffer, length, ClientTick);
        }
    }
}
