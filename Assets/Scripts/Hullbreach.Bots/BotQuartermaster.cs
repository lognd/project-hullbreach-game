using System;
using Hullbreach.Builder;
using Hullbreach.Core;
using Hullbreach.Net;
using Hullbreach.Ship;
using Hullbreach.Structure;

namespace Hullbreach.Bots
{
    // The economy half of play, done through the same server calls a player's build menu makes: it puts lost
    // parts of the bot's own design back from stock, and turns badly hurt blocks into a refund. It only
    // acts when the fight is quiet, because build mode freezes the ship for the tick it takes.
    // Scavenging crates and salvage is movement, so the pilot does that; this class never steers.
    // frob:ticket 01M7EXX75QK41EKPV5392HXCAX
    // frob:doc docs/reference/hullbreach-bots.md#botquartermaster
    public sealed class BotQuartermaster
    {
        readonly BotShip _design;
        uint _nextLookTick;
        uint _nextDeconstructTick;

        // Counters for reports and tests.
        // frob:doc docs/reference/hullbreach-bots.md#botquartermaster
        public int Placed { get; private set; }

        // frob:doc docs/reference/hullbreach-bots.md#botquartermaster
        public int Deconstructed { get; private set; }

        // frob:doc docs/reference/hullbreach-bots.md#botquartermaster
        public BotQuartermaster(BotShip design)
        {
            _design = design ?? throw new ArgumentNullException(nameof(design));
        }

        // Priority of a lost part: engines and guns first, then steering, then armour, then plain hull.
        static int Priority(byte type)
        {
            switch (type)
            {
                case BlockTypes.Thruster: return 0;
                case BlockTypes.Cannon: return 1;
                case BlockTypes.Fin: return 2;
                case BlockTypes.RetroThruster: return 3;
                case BlockTypes.Armor: return 4;
                default: return 5;
            }
        }

        // True when this tick was spent building: `sendInput` was called with the build-mode flag and the server
        // call made. False means nothing was done and the caller sends its normal flight input.
        // frob:doc docs/reference/hullbreach-bots.md#botquartermaster
        public bool Act(ServerSimulation sim, int peer, uint tick, BotObservation obs, Action<byte> sendInput)
        {
            if (tick < _nextLookTick) return false;
            _nextLookTick = tick + 10;
            if (!sim.Economy.Enabled || sim.Match == null || sim.Match.Phase != Hullbreach.Match.MatchPhase.Running) return false;
            if (!obs.Self.Alive || !Quiet(obs)) return false;
            if (sim.BuildCooldownTicksRemaining(peer) > 0) return false;
            if (!sim.Ships.TryGetValue(peer, out ShipBody ship)) return false;
            BlockGrid grid = ship.Grid;

            BotPlacement? want = null;
            int[] stock = obs.Stock;
            foreach (BotPlacement p in _design.Order)
            {
                if (p.TypeId == BlockTypes.Core || grid.Contains(BlockKey.Pack(p.X, p.Y))) continue;
                if (p.TypeId >= stock.Length || stock[p.TypeId] <= 0) continue;
                if (!want.HasValue || Priority(p.TypeId) < Priority(want.Value.TypeId)) want = p;
            }
            if (want.HasValue)
            {
                BotPlacement p = want.Value;
                sendInput(InputMessage.BuildModeBit);
                if (sim.CheckPlacement(peer, p.X, p.Y, p.TypeId, p.Modifiers, out _) == PlacementResult.Ok
                    && sim.PlaceBlock(peer, p.X, p.Y, p.TypeId, p.Modifiers) == PlacementResult.Ok)
                {
                    Placed++;
                    _nextLookTick = tick + 5;
                }
                else _nextLookTick = tick + 120; // nothing fits right now; look again later
                return true;
            }

            if (tick >= _nextDeconstructTick && grid.Count > 10)
            {
                int victim = int.MinValue, worst = 214;
                foreach (var kv in grid.All)
                {
                    byte t = kv.Value.TypeId;
                    if (t != BlockTypes.Hull && t != BlockTypes.Armor) continue;
                    if (kv.Value.Damage > worst && PlacementRules.CanRemove(grid, kv.Key)) { worst = kv.Value.Damage; victim = kv.Key; }
                }
                if (victim != int.MinValue)
                {
                    BlockKey.Unpack(victim, out int x, out int y);
                    sendInput(InputMessage.BuildModeBit);
                    if (sim.Deconstruct(peer, x, y) == PlacementResult.Ok) Deconstructed++;
                    _nextDeconstructTick = tick + 250;
                    return true;
                }
            }
            return false;
        }

        // Quiet: the enemy is far and no hostile shot is close.
        static bool Quiet(BotObservation obs)
        {
            if (obs.Enemy.Visible && Unity.Mathematics.math.distance(obs.Enemy.Position, obs.Self.Position) < 18f) return false;
            for (int i = 0; i < obs.Projectiles.Count; i++)
            {
                ProjectileView pr = obs.Projectiles[i];
                if (pr.Hostile && Unity.Mathematics.math.distance(pr.Position, obs.Self.Position) < 25f) return false;
            }
            return true;
        }
    }
}
