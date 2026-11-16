using Hullbreach.Net;
using Hullbreach.World;

namespace Hullbreach.Bots
{
    // The storm as the sparring bot sees it: the server's live ring, read on the match clock. The bot is driven with
    // absolute server ticks (pickups expire on that clock) but the ring counts from the tick the match started, so the
    // two differ by however long the build phase lasted. Null ring (no match yet, storm off) reports no storm.
    // frob:doc docs/reference/hullbreach-bots.md#sparringdriver
    public sealed class SimulationStormSource : IStormSource
    {
        readonly ServerSimulation _sim;

        // frob:doc docs/reference/hullbreach-bots.md#sparringdriver
        public SimulationStormSource(ServerSimulation sim) { _sim = sim; }

        // frob:doc docs/reference/hullbreach-bots.md#sparringdriver
        public bool TryGetState(uint tick, out StormState state)
        {
            state = default;
            StormZone zone = _sim.ActiveStorm;
            if (zone == null) return false;
            uint start = _sim.StormStartTick;
            state = zone.At((int)(tick >= start ? tick - start : 0u));
            return true;
        }
    }

    // What the offline "play vs bot" server runs each tick for its virtual opponent. Before the match is running the bot
    // sends build-mode input, so it stays frozen on its spawn like a player who is still building (a ship in flight mode
    // feels gravity: left alone through the build phase the bot fell onto the planet and sat there). Once the match runs it
    // is driven by a BotPeer with the live storm, exactly as ArenaMatch drives bots.
    // frob:doc docs/reference/hullbreach-bots.md#sparringdriver
    public sealed class SparringDriver
    {
        readonly BotPeer _peer;
        readonly IStormSource _storm;

        // frob:doc docs/reference/hullbreach-bots.md#sparringdriver
        public SparringDriver(ServerSimulation sim, BotPeer peer)
        {
            _peer = peer;
            _storm = new SimulationStormSource(sim);
        }

        // frob:doc docs/reference/hullbreach-bots.md#sparringdriver
        public static bool MatchRunning(ServerSimulation sim) => sim.Match != null && sim.Match.Phase == Hullbreach.Match.MatchPhase.Running;

        // Sends this tick's input; `tick` is the server's tick count and must not go backwards.
        // frob:doc docs/reference/hullbreach-bots.md#sparringdriver
        public void Tick(ServerSimulation sim, uint tick)
        {
            if (MatchRunning(sim)) _peer.Drive(sim, tick, _storm);
            else sim.SetInput(_peer.PeerId, new InputMessage((ushort)_peer.PeerId, tick, 0, 0, InputMessage.BuildModeBit));
        }
    }
}
