using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Hullbreach.NetCode.Entities
{
    /// <summary>The queue match id the next connection presents; set by HullbreachQueueConnect, empty for LAN and bot play.</summary>
    // frob:doc docs/netcode.md#queue-connect-flow
    public static class HullbreachQueueTicket
    {
        public static string Pending { get; private set; } = string.Empty;

        /// <summary>The platform skin slug this player presents with the ticket; empty for the default skin.</summary>
        public static string Skin { get; private set; } = string.Empty;

        public static void Set(string matchId, string skin = null)
        {
            Pending = matchId ?? string.Empty;
            Skin = skin ?? string.Empty;
        }

        public static void Clear()
        {
            Pending = string.Empty;
            Skin = string.Empty;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Clear();
    }

    /// <summary>Sends the JoinTicket RPC once per connection while a queue match id is pending.</summary>
    // frob:ticket 01M5XACF0K75AVMM0MCNDDWXNE
    // frob:doc docs/netcode.md#queue-connect-flow
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class HullbreachJoinTicketClientSystem : SystemBase
    {
        EntityQuery _pending;

        protected override void OnCreate()
        {
            _pending = GetEntityQuery(
                ComponentType.ReadOnly<NetworkId>(),
                ComponentType.Exclude<HullbreachJoinTicketSent>());
            RequireForUpdate<NetworkStreamDriver>();
        }

        protected override void OnUpdate()
        {
            string id = HullbreachQueueTicket.Pending;
            if (string.IsNullOrEmpty(id)) return;
            using var connections = _pending.ToEntityArray(Allocator.Temp);
            foreach (Entity connection in connections)
            {
                EntityManager.AddComponent<HullbreachJoinTicketSent>(connection);
                var rpc = new HullbreachJoinTicketRpc();
                rpc.MatchId.CopyFromTruncated(id);
                rpc.Skin.CopyFromTruncated(HullbreachQueueTicket.Skin);
                Entity request = EntityManager.CreateEntity();
                EntityManager.AddComponentData(request, rpc);
                EntityManager.AddComponentData(request, new SendRpcCommandRequest { TargetConnection = Entity.Null });
                Debug.Log("Hullbreach connect: presenting join ticket for match " + id + ".");
            }
        }
    }
}
