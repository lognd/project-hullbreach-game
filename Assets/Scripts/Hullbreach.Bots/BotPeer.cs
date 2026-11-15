using System;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Net;
using Hullbreach.Ship;
using Hullbreach.World;

namespace Hullbreach.Bots
{
    // Where the HUD would read the storm from. Kept as an interface so the bots compile and run with or
    // without the storm being wired into ServerSimulation: no storm means TryGetState returns false.
    // frob:ticket 01M7EXX75QK41EKPV5392HXCAX
    // frob:doc docs/reference/hullbreach-bots.md#istormsource
    public interface IStormSource
    {
        // The state at a server tick (match start is tick 0), or false when there is no storm.
        bool TryGetState(uint tick, out StormState state);
    }

    // A StormZone schedule as a bot-visible source; the same pure function every client already evaluates.
    // frob:doc docs/reference/hullbreach-bots.md#istormsource
    public sealed class StormZoneSource : IStormSource
    {
        readonly StormZone _zone;

        public StormZoneSource(StormZone zone) { _zone = zone ?? throw new ArgumentNullException(nameof(zone)); }

        public bool TryGetState(uint tick, out StormState state)
        {
            state = _zone.At((int)tick);
            return true;
        }
    }

    // Discards everything the server sends; a bot match has no real clients to tell.
    // frob:doc docs/reference/hullbreach-bots.md#botpeer
    public sealed class DiscardOutbox : IServerOutbox
    {
        public void Send(int peer, bool reliable, byte[] bytes, int length) { }
    }

    // The input boundary. A BotPeer stands where a network client would: each tick it reads the public
    // match state, asks its IShipInputSource what a player would press, and sends exactly the InputMessage a
    // client sends. The server cannot tell it from a person, and trusts it no further than a person.
    // frob:doc docs/reference/hullbreach-bots.md#botpeer
    public sealed class BotPeer
    {
        readonly int _peer;
        readonly int _enemyPeer;
        readonly IShipInputSource _source;
        readonly BotObservation _obs = new BotObservation();
        ShipCapabilities _caps;
        int _capsBlockCount = -1;
        int _capsAge;

        // frob:doc docs/reference/hullbreach-bots.md#botpeer
        public int PeerId => _peer;

        // frob:doc docs/reference/hullbreach-bots.md#botpeer
        public IShipInputSource Source => _source;

        // frob:doc docs/reference/hullbreach-bots.md#botpeer
        public BotPeer(int peerId, int enemyPeerId, IShipInputSource source)
        {
            _peer = peerId;
            _enemyPeer = enemyPeerId;
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        // Sends one input for this tick. `storm` may be null.
        // frob:doc docs/reference/hullbreach-bots.md#botpeer
        public void Drive(ServerSimulation sim, uint tick, IStormSource storm = null)
        {
            BotObservation obs = Observe(sim, tick, storm);
            ShipInput input = _source.Next(obs);
            sim.SetInput(_peer, InputMessage.FromFloats((ushort)_peer, tick, input.ThrustAxis, input.Steer, input.FirePressed));
        }

        // Builds what the player at this peer can see. Nothing here reads the enemy's input or design.
        // frob:doc docs/reference/hullbreach-bots.md#botpeer
        public BotObservation Observe(ServerSimulation sim, uint tick, IStormSource storm)
        {
            BotObservation obs = _obs;
            obs.Tick = tick;
            obs.Dt = 1f / sim.TickRate;
            obs.Projectiles.Clear();
            obs.Planets.Clear();

            var ships = sim.Ships;
            ships.TryGetValue(_peer, out ShipBody me);
            ships.TryGetValue(_enemyPeer, out ShipBody them);

            obs.Self = default;
            if (me != null) obs.Self = ViewSelf(me);
            obs.Enemy = them != null && them.Grid.Count > 0 ? ViewEnemy(them) : default;

            foreach (var p in sim.Projectiles)
                obs.Projectiles.Add(new ProjectileView { Position = p.Position, Velocity = p.Velocity, Hostile = p.OwnerPeer != _peer });

            for (int i = 0; sim.Gravity.TryGetPermanent(i, out GravityBody body); i++) obs.Planets.Add(body);
            obs.Bounds = sim.Bounds;

            obs.HasStorm = false;
            if (storm != null && storm.TryGetState(tick, out StormState state))
            {
                obs.HasStorm = true;
                obs.Storm = state;
            }
            return obs;
        }

        SelfView ViewSelf(ShipBody me)
        {
            if (me.Grid.Count != _capsBlockCount || ++_capsAge >= 50)
            {
                _caps = ShipCapabilities.Measure(me);
                _capsBlockCount = me.Grid.Count;
                _capsAge = 0;
            }
            return new SelfView
            {
                Position = me.LocalToWorld(me.Grid.Mass.CenterOfMass),
                Rotation = me.Rotation,
                Velocity = me.Velocity,
                AngularVelocity = me.AngularVelocity,
                CoreDamage = CoreDamage(me.Grid),
                Alive = Connectivity.ClassifyCore(me.Grid) != CoreState.Destroyed && me.Grid.Count > 0,
                MaxForwardAccel = _caps.MaxForwardAccel,
                MaxRetroAccel = _caps.MaxRetroAccel,
                MaxAngularAccel = _caps.MaxAngularAccel,
                AimLocal = _caps.AimLocal,
                HasCannon = _caps.HasCannon,
                CannonCooldownSeconds = me.CannonCooldown,
                Projectile = me.Projectile,
            };
        }

        static EnemyView ViewEnemy(ShipBody them)
        {
            float2 corePos = them.Position;
            if (them.Grid.CoreKey.HasValue) corePos = them.LocalToWorld(BlockGrid.CenterOf(them.Grid.CoreKey.Value));
            float radius = 1f;
            float2 com = them.Grid.Mass.CenterOfMass;
            foreach (var kv in them.Grid.All)
                radius = math.max(radius, math.length(BlockGrid.CenterOf(kv.Key) - com) + 0.5f);
            return new EnemyView
            {
                Visible = true,
                Position = them.LocalToWorld(com),
                Velocity = them.Velocity,
                Rotation = them.Rotation,
                AngularVelocity = them.AngularVelocity,
                CorePosition = corePos,
                CoreDamage = CoreDamage(them.Grid),
                Radius = radius,
            };
        }

        static float CoreDamage(BlockGrid grid)
        {
            if (!grid.CoreKey.HasValue || !grid.TryGet(grid.CoreKey.Value, out Block core)) return 1f;
            return core.Damage / 255f;
        }
    }
}
