using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Hullbreach.Core;
using Hullbreach.Net;

namespace Hullbreach.Game
{
    /// <summary>
    /// Runs a whole ServerSimulation + two ClientReplica instances
    /// in-process over a jittery LoopbackTransport, purely so a handoff
    /// engineer can see the netcode pipeline moving in the Editor without
    /// standing up a real transport. Deliberately tiny: it hardcodes two
    /// ship designs, drives one with WASD-style axes and fires the other's
    /// cannon on a timer, and renders each replica as one colored quad per
    /// block using runtime-generated GameObjects (no prefabs, matching
    /// ShipRenderer's own no-asset-dependency approach, but independent of
    /// it since ShipRenderer requires a ShipController/Rigidbody2D this
    /// demo's replica ships deliberately do not have).
    ///
    /// Does not touch DemoScene; drop this on an empty GameObject in any
    /// scene to see it run.
    /// </summary>
    public sealed class NetDemo : MonoBehaviour
    {
        [SerializeField] float tickRate = 50f;

        LoopbackTransport _hub;
        Hullbreach.Net.ITransport _serverTransport;
        Hullbreach.Net.ITransport _client1Transport;
        Hullbreach.Net.ITransport _client2Transport;
        int _serverId, _peerThruster, _peerCannon;

        ServerOutbox _outbox;
        ServerSimulation _server;
        ClientReplica _replica1;
        ClientReplica _replica2;

        readonly Dictionary<(ushort netId, int key), GameObject> _visuals = new Dictionary<(ushort, int), GameObject>();
        float _tickAccumulator;
        uint _clientTick;
        float _fireTimer;

        void Awake()
        {
            _hub = new LoopbackTransport(seed: 1) { DelayTicks = 1, JitterTicks = 2 };
            _serverTransport = _hub.CreateEndpoint(out _serverId);
            _client1Transport = _hub.CreateEndpoint(out _peerThruster);
            _client2Transport = _hub.CreateEndpoint(out _peerCannon);
            _hub.Connect(_serverId, _peerThruster);
            _hub.Connect(_serverId, _peerCannon);

            _outbox = new ServerOutbox();
            _server = new ServerSimulation(_outbox) { TickRate = tickRate };

            _server.Join(_peerThruster, Design(new[]
            {
                new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
                new SnapshotBlock(0, -1, BlockTypes.Thruster, 0, 0),
                new SnapshotBlock(0, 1, BlockTypes.Hull, 0, 0),
            }, new float2(-4f, 0f)));

            _server.Join(_peerCannon, Design(new[]
            {
                new SnapshotBlock(0, 0, BlockTypes.Core, 0, 0),
                new SnapshotBlock(0, 1, BlockTypes.Cannon, 0, 0),
            }, new float2(4f, 0f)));

            _replica1 = new ClientReplica();
            _replica2 = new ClientReplica();
        }

        static ShipSnapshot Design(SnapshotBlock[] blocks, float2 position)
            => new ShipSnapshot(0, 0, blocks, Quantization.PackPosition(position.x), Quantization.PackPosition(position.y), 0, 0, 0, 0);

        void FixedUpdate()
        {
            if (_server == null) return; // never true after Awake, but keeps this null-safe if reused elsewhere

            _tickAccumulator += Time.fixedDeltaTime;
            float dt = 1f / tickRate;
            while (_tickAccumulator >= dt)
            {
                _tickAccumulator -= dt;
                RunOneTick();
            }

            RefreshVisuals(_replica1);
            RefreshVisuals(_replica2);
        }

        void RunOneTick()
        {
            _clientTick++;
            _fireTimer -= 1f / tickRate;

            float thrust = Input.GetAxisRaw("Vertical");
            float steer = Input.GetAxisRaw("Horizontal");
            SendInput(_client1Transport, InputMessage.FromFloats((ushort)_peerThruster, _clientTick, thrust, steer, false));

            bool fire = _fireTimer <= 0f;
            if (fire) _fireTimer = 1f;
            SendInput(_client2Transport, InputMessage.FromFloats((ushort)_peerCannon, _clientTick, 0f, 0f, fire));

            var scratch = new byte[512];
            while (_serverTransport.TryReceive(out int from, scratch, out int length))
            {
                var r = new ByteReader(scratch);
                _server.SetInput(from, InputMessage.Read(ref r));
            }

            _server.Tick();

            foreach (var entry in _outbox.Entries)
            {
                if (entry.Reliable) _serverTransport.SendReliable(entry.Peer, entry.Bytes);
                else _serverTransport.SendUnreliable(entry.Peer, entry.Bytes);
            }
            _outbox.Clear();

            _hub.Tick();

            Drain(_client1Transport, _replica1);
            Drain(_client2Transport, _replica2);
        }

        void SendInput(Hullbreach.Net.ITransport transport, InputMessage input)
        {
            var buf = new byte[16];
            var w = new ByteWriter(buf);
            input.Write(ref w);
            transport.SendUnreliable(_serverId, new System.ReadOnlySpan<byte>(buf, 0, w.Position));
        }

        void Drain(Hullbreach.Net.ITransport transport, ClientReplica replica)
        {
            var buf = new byte[1024];
            while (transport.TryReceive(out _, buf, out int length))
                replica.ApplyReceived(buf, length, _clientTick);
        }

        /// <summary>Positions one small colored quad per block of every
        /// replica ship known so far, creating them lazily and never
        /// touching a block's visual once placed beyond moving it with its
        /// ship: cheap, and good enough for a handoff demo.</summary>
        void RefreshVisuals(ClientReplica replica)
        {
            if (replica == null) return;
            foreach (var kv in replica.Ships)
            {
                ushort netId = kv.Key;
                var ship = kv.Value;
                foreach (var block in ship.Grid.All)
                {
                    var visKey = (netId, block.Key);
                    if (!_visuals.TryGetValue(visKey, out var go) || go == null)
                    {
                        go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        go.name = $"NetDemo.Block[{netId}:{block.Key}]";
                        Object.Destroy(go.GetComponent<Collider>());
                        go.transform.SetParent(transform, worldPositionStays: false);
                        _visuals[visKey] = go;
                    }
                    float2 local = BlockGrid.CenterOf(block.Key);
                    float2 world = ship.LocalToWorld(local);
                    go.transform.position = new Vector3(world.x, world.y, 0f);
                    go.transform.rotation = Quaternion.Euler(0f, 0f, ship.Rotation * Mathf.Rad2Deg);
                }
            }
        }
    }
}
