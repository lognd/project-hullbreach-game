using System;

namespace Hullbreach.Net
{
    // Plain-C# host driver: pumps an ITransport into ServerSimulation at a fixed tick, no Unity.
    // frob:doc docs/reference/hullbreach-net.md#serverhost
    public sealed class ServerHost
    {
        // A stall longer than this many ticks is skipped, not replayed, so a hiccup cannot spiral.
        // frob:doc docs/reference/hullbreach-net.md#serverhost
        public const int MaxCatchUpTicks = 5;

        // Comfortably above any client->server message and below a typical UDP MTU.
        const int MaxPayloadBytes = 1200;

        readonly ITransport _transport;
        readonly ServerOutbox _outbox = new ServerOutbox();
        readonly byte[] _receiveBuffer = new byte[MaxPayloadBytes];
        readonly float _tickRate;

        // Host time is tracked as a total so tick boundaries never drift from float accumulation.
        double _hostSeconds;
        long _scheduledTicks;

        // frob:doc docs/reference/hullbreach-net.md#serverhost
        public ServerSimulation Simulation { get; }

        // frob:doc docs/reference/hullbreach-net.md#serverhost
        public long TicksRun { get; private set; }

        // frob:doc docs/reference/hullbreach-net.md#serverhost
        public ServerHost(ITransport transport, float tickRate = 50f)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _tickRate = tickRate;
            Simulation = new ServerSimulation(_outbox) { TickRate = tickRate };
        }

        // Seconds of host time until the next tick is due; never negative.
        // frob:doc docs/reference/hullbreach-net.md#serverhost
        public double SecondsUntilNextTick => Math.Max(0.0, (_scheduledTicks + 1) / (double)_tickRate - _hostSeconds);

        // The design arrives from the lobby/handshake (T-0053); the join snapshots go out at once.
        // frob:doc docs/reference/hullbreach-net.md#serverhost
        public void Join(int peer, ShipSnapshot design)
        {
            Simulation.Join(peer, design);
            FlushOutbox();
            NetLog.Write($"server host: peer {peer} joined with {design.Blocks.Length} blocks");
        }

        // frob:doc docs/reference/hullbreach-net.md#serverhost
        public void Leave(int peer)
        {
            Simulation.Leave(peer);
            NetLog.Write($"server host: peer {peer} left");
        }

        // Runs every tick that elapsed host time makes due and returns how many ran.
        // frob:doc docs/reference/hullbreach-net.md#serverhost
        public int Advance(double elapsedSeconds)
        {
            _hostSeconds += Math.Max(0.0, elapsedSeconds);

            // The epsilon absorbs float error so exactly N * TickRate seconds yields N ticks.
            long due = (long)Math.Floor(_hostSeconds * _tickRate + 1e-6) - _scheduledTicks;
            if (due > MaxCatchUpTicks)
            {
                NetLog.Write($"server host: stalled, skipping {due - MaxCatchUpTicks} of {due} due ticks");
                _scheduledTicks += due - MaxCatchUpTicks;
                due = MaxCatchUpTicks;
            }

            for (long i = 0; i < due; i++)
            {
                StepOnce();
                _scheduledTicks++;
            }
            return (int)due;
        }

        // One tick: drain inbound, simulate, forward the outbox. Public so tests can single-step.
        // frob:doc docs/reference/hullbreach-net.md#serverhost
        public void StepOnce()
        {
            PumpInbound();
            Simulation.Tick();
            FlushOutbox();
            TicksRun++;
        }

        // The loop a process entry point calls; clock and sleep are injected so tests need no wall time.
        // frob:doc docs/reference/hullbreach-net.md#serverhost
        public void Run(Func<double> clockSeconds, Action<double> sleepSeconds, Func<bool> shouldStop)
        {
            NetLog.Write($"server host: running at {_tickRate} Hz");
            double last = clockSeconds();
            while (!shouldStop())
            {
                double now = clockSeconds();
                Advance(now - last);
                last = now;
                sleepSeconds(SecondsUntilNextTick);
            }
            NetLog.Write($"server host: stopped after {TicksRun} ticks");
        }

        void PumpInbound()
        {
            while (_transport.TryReceive(out int from, _receiveBuffer, out int length))
                Dispatch(from, length);
        }

        // A client controls every byte here, so anything malformed is dropped, never thrown.
        void Dispatch(int from, int length)
        {
            if (length < 1)
            {
                NetLog.Write($"server host: dropped empty payload from peer {from}");
                return;
            }

            var kind = (MessageKind)_receiveBuffer[0];
            switch (kind)
            {
                case MessageKind.Input:
                    if (length != InputMessage.ByteSize)
                    {
                        NetLog.Write($"server host: dropped Input of {length} bytes from peer {from}");
                        return;
                    }
                    var reader = new ByteReader(_receiveBuffer);
                    // The transport peer id, not the claimed NetId, picks the ship: a client cannot steer another's.
                    Simulation.SetInput(from, InputMessage.Read(ref reader));
                    return;
                case MessageKind.BuildRequest:
                    if (length != BuildRequest.ByteSize)
                    {
                        NetLog.Write($"server host: dropped BuildRequest of {length} bytes from peer {from}");
                        return;
                    }
                    var buildReader = new ByteReader(_receiveBuffer);
                    // Refusals are logged by the simulation; the client learns of a success from the BlockPlaced broadcast.
                    Simulation.TryPlaceBlock(from, BuildRequest.Read(ref buildReader), out _, out _);
                    return;
                default:
                    NetLog.Write($"server host: dropped client->server kind {(byte)kind} from peer {from}");
                    return;
            }
        }

        void FlushOutbox()
        {
            foreach (var entry in _outbox.Entries)
            {
                if (entry.Reliable) _transport.SendReliable(entry.Peer, entry.Bytes);
                else _transport.SendUnreliable(entry.Peer, entry.Bytes);
            }
            _outbox.Clear();
        }
    }
}
