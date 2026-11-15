using System.Collections.Generic;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Net;
using Hullbreach.Ship;
using Hullbreach.World;

namespace Hullbreach.Bots
{
    // Stand-in for the server applying StormZone damage, so balance runs can include the storm before
    // ServerSimulation does it itself. Each tick every block outside the ring takes StormState.DamageFor
    // (the core only in sudden death), accumulated as a fraction so slow phases still bite. Non-core blocks
    // stop at 254 here: removing a block needs the server's solver and topology bookkeeping, which this
    // overlay deliberately does not reach into. Retire it once the server owns storm damage.
    // frob:ticket 01M7EXX75QK41EKPV5392HXCAX
    // frob:doc docs/reference/hullbreach-bots.md#stormoverlay
    public sealed class StormOverlay
    {
        readonly StormZone _zone;
        readonly Dictionary<long, float> _carry = new Dictionary<long, float>();
        readonly Dictionary<int, float> _coreDamage = new Dictionary<int, float>();
        readonly Dictionary<int, float> _blockDamage = new Dictionary<int, float>();

        // frob:doc docs/reference/hullbreach-bots.md#stormoverlay
        public StormOverlay(StormZone zone) { _zone = zone; }

        // Total damage the storm has dealt to a peer's core.
        // frob:doc docs/reference/hullbreach-bots.md#stormoverlay
        public float CoreDamageBy(int peer) => _coreDamage.TryGetValue(peer, out float v) ? v : 0f;

        // Total storm damage dealt to all of a peer's blocks, core included.
        // frob:doc docs/reference/hullbreach-bots.md#stormoverlay
        public float TotalDamageTo(int peer) => _blockDamage.TryGetValue(peer, out float v) ? v : 0f;

        // frob:doc docs/reference/hullbreach-bots.md#stormoverlay
        public void Apply(ServerSimulation sim, uint tick, params int[] peers)
        {
            StormState state = _zone.At((int)tick);
            if (state.DamagePerTick <= 0f) return;
            var ships = sim.Ships;
            foreach (int peer in peers)
            {
                if (!ships.TryGetValue(peer, out ShipBody ship) || ship.Grid.Count == 0) continue;
                var keys = ship.Grid.SortedKeys.ToArray();
                foreach (int key in keys)
                {
                    if (!ship.Grid.TryGet(key, out Block block)) continue;
                    bool isCore = ship.Grid.CoreKey.HasValue && key == ship.Grid.CoreKey.Value;
                    float dmg = state.DamageFor(ship.LocalToWorld(BlockGrid.CenterOf(key)), isCore);
                    if (dmg <= 0f) continue;

                    long id = ((long)peer << 32) | (uint)key;
                    _carry.TryGetValue(id, out float carry);
                    carry += dmg;
                    int whole = (int)carry;
                    carry -= whole;
                    _carry[id] = carry;
                    if (whole <= 0) continue;

                    int cap = isCore ? 255 : 254;
                    int next = math.min(cap, block.Damage + whole);
                    int applied = next - block.Damage;
                    if (applied <= 0) continue;
                    ship.Grid.TrySet(key, block.WithDamage((byte)next));
                    _blockDamage[peer] = TotalDamageTo(peer) + applied;
                    if (isCore) _coreDamage[peer] = CoreDamageBy(peer) + applied;
                }
            }
        }
    }
}
