using System;

namespace Hullbreach.Net
{
    // Wire formats. Governing rule and message table: docs/netcode.md.
    // frob:doc docs/reference/hullbreach-net.md#netmessages
    public static class NetMessages
    {
        // See Wire.cs/ITransport/ServerSimulation/ClientReplica for the pieces.
    }

    // Kept as its own byte, not a discriminated union, for single-switch dispatch.
    // frob:doc docs/reference/hullbreach-net.md#messagekind
    public enum MessageKind : byte
    {
        Input = 1,
        ShipSnapshot = 2,
        ShipState = 3,
        BlockPlaced = 4,
        BlockDestroyed = 5,
        FragmentSpawned = 6,
        BlockDamaged = 7,
        PowerupApplied = 8,
        GravityWellSpawned = 9,
    }

    // Client -> server, unreliable: this tick's intent. Latest-wins on drop.
    // frob:doc docs/reference/hullbreach-net.md#inputmessage
    public readonly struct InputMessage
    {
        // frob:doc docs/reference/hullbreach-net.md#inputmessage
        public readonly ushort NetId;

        // frob:doc docs/reference/hullbreach-net.md#inputmessage
        public readonly uint Tick;

        // Quantized -127..127, unpacked to -1f..1f by /127f.
        // frob:doc docs/reference/hullbreach-net.md#inputmessage
        public readonly sbyte ThrustAxis;

        // Quantized -127..127, unpacked to -1f..1f by /127f.
        // frob:doc docs/reference/hullbreach-net.md#inputmessage
        public readonly sbyte Steer;

        // Bit0 = fire pressed. Other bits reserved.
        // frob:doc docs/reference/hullbreach-net.md#inputmessage
        public readonly byte Flags;

        // frob:doc docs/reference/hullbreach-net.md#inputmessage
        public const byte FireBit = 0x01;

        // frob:doc docs/reference/hullbreach-net.md#inputmessage
        public InputMessage(ushort netId, uint tick, sbyte thrustAxis, sbyte steer, byte flags)
        {
            NetId = netId;
            Tick = tick;
            ThrustAxis = thrustAxis;
            Steer = steer;
            Flags = flags;
        }

        // frob:doc docs/reference/hullbreach-net.md#inputmessage
        public static InputMessage FromFloats(ushort netId, uint tick, float thrustAxis, float steer, bool firePressed)
        {
            sbyte QuantizeAxis(float v)
            {
                float clamped = Math.Max(-1f, Math.Min(1f, v));
                return (sbyte)Math.Round(clamped * 127f, MidpointRounding.AwayFromZero);
            }
            byte flags = firePressed ? FireBit : (byte)0;
            return new InputMessage(netId, tick, QuantizeAxis(thrustAxis), QuantizeAxis(steer), flags);
        }

        // frob:doc docs/reference/hullbreach-net.md#inputmessage
        public float ThrustAxisFloat => ThrustAxis / 127f;

        // frob:doc docs/reference/hullbreach-net.md#inputmessage
        public float SteerFloat => Steer / 127f;

        // frob:doc docs/reference/hullbreach-net.md#inputmessage
        public bool FirePressed => (Flags & FireBit) != 0;

        // frob:doc docs/reference/hullbreach-net.md#inputmessage
        public void Write(ref ByteWriter w)
        {
            w.WriteU8((byte)MessageKind.Input);
            w.WriteU16(NetId);
            w.WriteU32(Tick);
            w.WriteI8(ThrustAxis);
            w.WriteI8(Steer);
            w.WriteU8(Flags);
        }

        // Reads the MessageKind byte too, mirroring Write.
        // frob:doc docs/reference/hullbreach-net.md#inputmessage
        public static InputMessage Read(ref ByteReader r)
        {
            r.ReadU8(); // MessageKind
            ushort netId = r.ReadU16();
            uint tick = r.ReadU32();
            sbyte thrust = r.ReadI8();
            sbyte steer = r.ReadI8();
            byte flags = r.ReadU8();
            return new InputMessage(netId, tick, thrust, steer, flags);
        }
    }

    // One block as carried by ShipSnapshot: grid-local position (fits an
    // sbyte per BlockKey's -128..127 range), type, modifiers and damage.
    // frob:doc docs/reference/hullbreach-net.md#snapshotblock
    public readonly struct SnapshotBlock
    {
        // frob:doc docs/reference/hullbreach-net.md#snapshotblock
        public readonly sbyte X;

        // frob:doc docs/reference/hullbreach-net.md#snapshotblock
        public readonly sbyte Y;

        // frob:doc docs/reference/hullbreach-net.md#snapshotblock
        public readonly byte TypeId;

        // frob:doc docs/reference/hullbreach-net.md#snapshotblock
        public readonly byte Mods;

        // frob:doc docs/reference/hullbreach-net.md#snapshotblock
        public readonly byte Damage;

        // frob:doc docs/reference/hullbreach-net.md#snapshotblock
        public SnapshotBlock(sbyte x, sbyte y, byte typeId, byte mods, byte damage)
        {
            X = x;
            Y = y;
            TypeId = typeId;
            Mods = mods;
            Damage = damage;
        }
    }

    // Server -> client, reliable, sent once on join/respawn: the full design.
    // frob:doc docs/reference/hullbreach-net.md#shipsnapshot
    public readonly struct ShipSnapshot
    {
        // frob:doc docs/reference/hullbreach-net.md#shipsnapshot
        public readonly uint Sequence;

        // frob:doc docs/reference/hullbreach-net.md#shipsnapshot
        public readonly ushort NetId;

        // frob:doc docs/reference/hullbreach-net.md#shipsnapshot
        public readonly SnapshotBlock[] Blocks;

        // frob:doc docs/reference/hullbreach-net.md#shipsnapshot
        public readonly short Px;

        // frob:doc docs/reference/hullbreach-net.md#shipsnapshot
        public readonly short Py;

        // frob:doc docs/reference/hullbreach-net.md#shipsnapshot
        public readonly ushort Rot;

        // frob:doc docs/reference/hullbreach-net.md#shipsnapshot
        public readonly short Vx;

        // frob:doc docs/reference/hullbreach-net.md#shipsnapshot
        public readonly short Vy;

        // frob:doc docs/reference/hullbreach-net.md#shipsnapshot
        public readonly ushort Av;

        // frob:doc docs/reference/hullbreach-net.md#shipsnapshot
        public ShipSnapshot(uint sequence, ushort netId, SnapshotBlock[] blocks,
                             short px, short py, ushort rot, short vx, short vy, ushort av)
        {
            Sequence = sequence;
            NetId = netId;
            Blocks = blocks;
            Px = px;
            Py = py;
            Rot = rot;
            Vx = vx;
            Vy = vy;
            Av = av;
        }

        // So a caller can size its send buffer without writing twice.
        // frob:doc docs/reference/hullbreach-net.md#shipsnapshot
        public int ByteSize => 1 + 4 + 2 + 2 + (Blocks.Length * 5) + 2 + 2 + 2 + 2 + 2 + 2;

        // frob:doc docs/reference/hullbreach-net.md#shipsnapshot
        public void Write(ref ByteWriter w)
        {
            w.WriteU8((byte)MessageKind.ShipSnapshot);
            w.WriteU32(Sequence);
            w.WriteU16(NetId);
            w.WriteU16((ushort)Blocks.Length);
            for (int i = 0; i < Blocks.Length; i++)
            {
                var b = Blocks[i];
                w.WriteI8(b.X);
                w.WriteI8(b.Y);
                w.WriteU8(b.TypeId);
                w.WriteU8(b.Mods);
                w.WriteU8(b.Damage);
            }
            w.WriteI16(Px);
            w.WriteI16(Py);
            w.WriteU16(Rot);
            w.WriteI16(Vx);
            w.WriteI16(Vy);
            w.WriteU16(Av);
        }

        // frob:doc docs/reference/hullbreach-net.md#shipsnapshot
        public static ShipSnapshot Read(ref ByteReader r)
        {
            r.ReadU8(); // MessageKind
            uint seq = r.ReadU32();
            ushort netId = r.ReadU16();
            ushort count = r.ReadU16();
            var blocks = new SnapshotBlock[count];
            for (int i = 0; i < count; i++)
            {
                sbyte x = r.ReadI8();
                sbyte y = r.ReadI8();
                byte typeId = r.ReadU8();
                byte mods = r.ReadU8();
                byte damage = r.ReadU8();
                blocks[i] = new SnapshotBlock(x, y, typeId, mods, damage);
            }
            short px = r.ReadI16();
            short py = r.ReadI16();
            ushort rot = r.ReadU16();
            short vx = r.ReadI16();
            short vy = r.ReadI16();
            ushort av = r.ReadU16();
            return new ShipSnapshot(seq, netId, blocks, px, py, rot, vx, vy, av);
        }
    }

    // Server -> client, unreliable, ~30-50 Hz: this tick's pose.
    // frob:doc docs/reference/hullbreach-net.md#shipstate
    public readonly struct ShipState
    {
        // frob:doc docs/reference/hullbreach-net.md#shipstate
        public readonly ushort NetId;

        // frob:doc docs/reference/hullbreach-net.md#shipstate
        public readonly short Px;

        // frob:doc docs/reference/hullbreach-net.md#shipstate
        public readonly short Py;

        // frob:doc docs/reference/hullbreach-net.md#shipstate
        public readonly ushort Rot;

        // frob:doc docs/reference/hullbreach-net.md#shipstate
        public readonly short Vx;

        // frob:doc docs/reference/hullbreach-net.md#shipstate
        public readonly short Vy;

        // frob:doc docs/reference/hullbreach-net.md#shipstate
        public readonly ushort Av;

        // frob:doc docs/reference/hullbreach-net.md#shipstate
        public const int ByteSize = 1 + 2 + 2 + 2 + 2 + 2 + 2 + 2;

        // frob:doc docs/reference/hullbreach-net.md#shipstate
        public ShipState(ushort netId, short px, short py, ushort rot, short vx, short vy, ushort av)
        {
            NetId = netId;
            Px = px;
            Py = py;
            Rot = rot;
            Vx = vx;
            Vy = vy;
            Av = av;
        }

        // frob:doc docs/reference/hullbreach-net.md#shipstate
        public void Write(ref ByteWriter w)
        {
            w.WriteU8((byte)MessageKind.ShipState);
            w.WriteU16(NetId);
            w.WriteI16(Px);
            w.WriteI16(Py);
            w.WriteU16(Rot);
            w.WriteI16(Vx);
            w.WriteI16(Vy);
            w.WriteU16(Av);
        }

        // frob:doc docs/reference/hullbreach-net.md#shipstate
        public static ShipState Read(ref ByteReader r)
        {
            r.ReadU8();
            ushort netId = r.ReadU16();
            short px = r.ReadI16();
            short py = r.ReadI16();
            ushort rot = r.ReadU16();
            short vx = r.ReadI16();
            short vy = r.ReadI16();
            ushort av = r.ReadU16();
            return new ShipState(netId, px, py, rot, vx, vy, av);
        }
    }

    // Server -> client, reliable ordered: a block was placed.
    // frob:doc docs/reference/hullbreach-net.md#blockplaced
    public readonly struct BlockPlaced
    {
        // frob:doc docs/reference/hullbreach-net.md#blockplaced
        public readonly uint Sequence;

        // frob:doc docs/reference/hullbreach-net.md#blockplaced
        public readonly ushort NetId;

        // frob:doc docs/reference/hullbreach-net.md#blockplaced
        public readonly sbyte X;

        // frob:doc docs/reference/hullbreach-net.md#blockplaced
        public readonly sbyte Y;

        // frob:doc docs/reference/hullbreach-net.md#blockplaced
        public readonly byte TypeId;

        // frob:doc docs/reference/hullbreach-net.md#blockplaced
        public readonly byte Mods;

        // frob:doc docs/reference/hullbreach-net.md#blockplaced
        public BlockPlaced(uint sequence, ushort netId, sbyte x, sbyte y, byte typeId, byte mods)
        {
            Sequence = sequence;
            NetId = netId;
            X = x;
            Y = y;
            TypeId = typeId;
            Mods = mods;
        }

        // frob:doc docs/reference/hullbreach-net.md#blockplaced
        public void Write(ref ByteWriter w)
        {
            w.WriteU8((byte)MessageKind.BlockPlaced);
            w.WriteU32(Sequence);
            w.WriteU16(NetId);
            w.WriteI8(X);
            w.WriteI8(Y);
            w.WriteU8(TypeId);
            w.WriteU8(Mods);
        }

        // frob:doc docs/reference/hullbreach-net.md#blockplaced
        public static BlockPlaced Read(ref ByteReader r)
        {
            r.ReadU8();
            uint seq = r.ReadU32();
            ushort netId = r.ReadU16();
            sbyte x = r.ReadI8();
            sbyte y = r.ReadI8();
            byte typeId = r.ReadU8();
            byte mods = r.ReadU8();
            return new BlockPlaced(seq, netId, x, y, typeId, mods);
        }
    }

    // Server -> client, reliable ordered: a block died; see the reference page.
    // frob:doc docs/reference/hullbreach-net.md#blockdestroyed
    public readonly struct BlockDestroyed
    {
        // frob:doc docs/reference/hullbreach-net.md#blockdestroyed
        public readonly uint Sequence;

        // frob:doc docs/reference/hullbreach-net.md#blockdestroyed
        public readonly ushort NetId;

        // frob:doc docs/reference/hullbreach-net.md#blockdestroyed
        public readonly sbyte X;

        // frob:doc docs/reference/hullbreach-net.md#blockdestroyed
        public readonly sbyte Y;

        // frob:doc docs/reference/hullbreach-net.md#blockdestroyed
        public BlockDestroyed(uint sequence, ushort netId, sbyte x, sbyte y)
        {
            Sequence = sequence;
            NetId = netId;
            X = x;
            Y = y;
        }

        // frob:doc docs/reference/hullbreach-net.md#blockdestroyed
        public void Write(ref ByteWriter w)
        {
            w.WriteU8((byte)MessageKind.BlockDestroyed);
            w.WriteU32(Sequence);
            w.WriteU16(NetId);
            w.WriteI8(X);
            w.WriteI8(Y);
        }

        // frob:doc docs/reference/hullbreach-net.md#blockdestroyed
        public static BlockDestroyed Read(ref ByteReader r)
        {
            r.ReadU8();
            uint seq = r.ReadU32();
            ushort netId = r.ReadU16();
            sbyte x = r.ReadI8();
            sbyte y = r.ReadI8();
            return new BlockDestroyed(seq, netId, x, y);
        }
    }

    // Server -> client, reliable ordered: a detached component is spawned.
    // frob:doc docs/reference/hullbreach-net.md#fragmentspawned
    public readonly struct FragmentSpawned
    {
        // frob:doc docs/reference/hullbreach-net.md#fragmentspawned
        public readonly uint Sequence;

        // frob:doc docs/reference/hullbreach-net.md#fragmentspawned
        public readonly ushort ParentId;

        // frob:doc docs/reference/hullbreach-net.md#fragmentspawned
        public readonly ushort NewId;

        // frob:doc docs/reference/hullbreach-net.md#fragmentspawned
        public readonly short Px;

        // frob:doc docs/reference/hullbreach-net.md#fragmentspawned
        public readonly short Py;

        // frob:doc docs/reference/hullbreach-net.md#fragmentspawned
        public readonly ushort Rot;

        // frob:doc docs/reference/hullbreach-net.md#fragmentspawned
        public readonly short Vx;

        // frob:doc docs/reference/hullbreach-net.md#fragmentspawned
        public readonly short Vy;

        // frob:doc docs/reference/hullbreach-net.md#fragmentspawned
        public readonly ushort Av;

        // frob:doc docs/reference/hullbreach-net.md#fragmentspawned
        public FragmentSpawned(uint sequence, ushort parentId, ushort newId,
                                short px, short py, ushort rot, short vx, short vy, ushort av)
        {
            Sequence = sequence;
            ParentId = parentId;
            NewId = newId;
            Px = px;
            Py = py;
            Rot = rot;
            Vx = vx;
            Vy = vy;
            Av = av;
        }

        // frob:doc docs/reference/hullbreach-net.md#fragmentspawned
        public void Write(ref ByteWriter w)
        {
            w.WriteU8((byte)MessageKind.FragmentSpawned);
            w.WriteU32(Sequence);
            w.WriteU16(ParentId);
            w.WriteU16(NewId);
            w.WriteI16(Px);
            w.WriteI16(Py);
            w.WriteU16(Rot);
            w.WriteI16(Vx);
            w.WriteI16(Vy);
            w.WriteU16(Av);
        }

        // frob:doc docs/reference/hullbreach-net.md#fragmentspawned
        public static FragmentSpawned Read(ref ByteReader r)
        {
            r.ReadU8();
            uint seq = r.ReadU32();
            ushort parentId = r.ReadU16();
            ushort newId = r.ReadU16();
            short px = r.ReadI16();
            short py = r.ReadI16();
            ushort rot = r.ReadU16();
            short vx = r.ReadI16();
            short vy = r.ReadI16();
            ushort av = r.ReadU16();
            return new FragmentSpawned(seq, parentId, newId, px, py, rot, vx, vy, av);
        }
    }

    // Server -> client, reliable ordered: damage, a CAUSE the client can't derive.
    // frob:doc docs/reference/hullbreach-net.md#blockdamaged
    public readonly struct BlockDamaged
    {
        // frob:doc docs/reference/hullbreach-net.md#blockdamaged
        public readonly uint Sequence;

        // frob:doc docs/reference/hullbreach-net.md#blockdamaged
        public readonly ushort NetId;

        // frob:doc docs/reference/hullbreach-net.md#blockdamaged
        public readonly sbyte X;

        // frob:doc docs/reference/hullbreach-net.md#blockdamaged
        public readonly sbyte Y;

        // frob:doc docs/reference/hullbreach-net.md#blockdamaged
        public readonly byte Damage;

        // frob:doc docs/reference/hullbreach-net.md#blockdamaged
        public BlockDamaged(uint sequence, ushort netId, sbyte x, sbyte y, byte damage)
        {
            Sequence = sequence;
            NetId = netId;
            X = x;
            Y = y;
            Damage = damage;
        }

        // frob:doc docs/reference/hullbreach-net.md#blockdamaged
        public void Write(ref ByteWriter w)
        {
            w.WriteU8((byte)MessageKind.BlockDamaged);
            w.WriteU32(Sequence);
            w.WriteU16(NetId);
            w.WriteI8(X);
            w.WriteI8(Y);
            w.WriteU8(Damage);
        }

        // frob:doc docs/reference/hullbreach-net.md#blockdamaged
        public static BlockDamaged Read(ref ByteReader r)
        {
            r.ReadU8();
            uint seq = r.ReadU32();
            ushort netId = r.ReadU16();
            sbyte x = r.ReadI8();
            sbyte y = r.ReadI8();
            byte damage = r.ReadU8();
            return new BlockDamaged(seq, netId, x, y, damage);
        }
    }

    // Server -> client, reliable ordered: a powerup transform landed.
    // frob:doc docs/reference/hullbreach-net.md#powerupapplied
    public readonly struct PowerupApplied
    {
        // frob:doc docs/reference/hullbreach-net.md#powerupapplied
        public readonly uint Sequence;

        // frob:doc docs/reference/hullbreach-net.md#powerupapplied
        public readonly ushort NetId;

        // frob:doc docs/reference/hullbreach-net.md#powerupapplied
        public readonly sbyte X;

        // frob:doc docs/reference/hullbreach-net.md#powerupapplied
        public readonly sbyte Y;

        // frob:doc docs/reference/hullbreach-net.md#powerupapplied
        public readonly byte Variant;

        // frob:doc docs/reference/hullbreach-net.md#powerupapplied
        public readonly ushort Seconds10;

        // frob:doc docs/reference/hullbreach-net.md#powerupapplied
        public PowerupApplied(uint sequence, ushort netId, sbyte x, sbyte y, byte variant, ushort seconds10)
        {
            Sequence = sequence;
            NetId = netId;
            X = x;
            Y = y;
            Variant = variant;
            Seconds10 = seconds10;
        }

        // frob:doc docs/reference/hullbreach-net.md#powerupapplied
        public float Seconds => Seconds10 / 10f;

        // frob:doc docs/reference/hullbreach-net.md#powerupapplied
        public void Write(ref ByteWriter w)
        {
            w.WriteU8((byte)MessageKind.PowerupApplied);
            w.WriteU32(Sequence);
            w.WriteU16(NetId);
            w.WriteI8(X);
            w.WriteI8(Y);
            w.WriteU8(Variant);
            w.WriteU16(Seconds10);
        }

        // frob:doc docs/reference/hullbreach-net.md#powerupapplied
        public static PowerupApplied Read(ref ByteReader r)
        {
            r.ReadU8();
            uint seq = r.ReadU32();
            ushort netId = r.ReadU16();
            sbyte x = r.ReadI8();
            sbyte y = r.ReadI8();
            byte variant = r.ReadU8();
            ushort seconds10 = r.ReadU16();
            return new PowerupApplied(seq, netId, x, y, variant, seconds10);
        }
    }

    // Server -> client, reliable ordered: a temporary gravity well/anti-well
    // (IWorldSink.AddTemporaryGravity) was dropped into the world field.
    // frob:doc docs/reference/hullbreach-net.md#gravitywellspawned
    public readonly struct GravityWellSpawned
    {
        // frob:doc docs/reference/hullbreach-net.md#gravitywellspawned
        public readonly short Px;

        // frob:doc docs/reference/hullbreach-net.md#gravitywellspawned
        public readonly short Py;

        // frob:doc docs/reference/hullbreach-net.md#gravitywellspawned
        public readonly short Mu;

        // frob:doc docs/reference/hullbreach-net.md#gravitywellspawned
        public readonly byte Radius;

        // frob:doc docs/reference/hullbreach-net.md#gravitywellspawned
        public readonly ushort Seconds10;

        // 1/16 of a world Mu unit per Mu16 unit: roughly +-2048 range at
        // 1/16 resolution, covering both gravity wells and anti-wells.
        // frob:doc docs/reference/hullbreach-net.md#gravitywellspawned
        public const float MuScale = 16f;

        // frob:doc docs/reference/hullbreach-net.md#gravitywellspawned
        public GravityWellSpawned(short px, short py, short mu, byte radius, ushort seconds10)
        {
            Px = px;
            Py = py;
            Mu = mu;
            Radius = radius;
            Seconds10 = seconds10;
        }

        // frob:doc docs/reference/hullbreach-net.md#gravitywellspawned
        public static GravityWellSpawned FromFloats(float px, float py, float mu, float radius, float seconds)
        {
            short qmu = (short)Math.Round(Math.Max(short.MinValue, Math.Min(short.MaxValue, mu * MuScale)), MidpointRounding.AwayFromZero);
            byte qradius = (byte)Math.Max(0, Math.Min(255, Math.Round(radius, MidpointRounding.AwayFromZero)));
            ushort qseconds10 = (ushort)Math.Max(0, Math.Min(ushort.MaxValue, Math.Round(seconds * 10f, MidpointRounding.AwayFromZero)));
            return new GravityWellSpawned(Quantization.PackPosition(px), Quantization.PackPosition(py), qmu, qradius, qseconds10);
        }

        // frob:doc docs/reference/hullbreach-net.md#gravitywellspawned
        public float MuFloat => Mu / MuScale;

        // frob:doc docs/reference/hullbreach-net.md#gravitywellspawned
        public float RadiusFloat => Radius;

        // frob:doc docs/reference/hullbreach-net.md#gravitywellspawned
        public float SecondsFloat => Seconds10 / 10f;

        // frob:doc docs/reference/hullbreach-net.md#gravitywellspawned
        public float PxFloat => Quantization.UnpackPosition(Px);

        // frob:doc docs/reference/hullbreach-net.md#gravitywellspawned
        public float PyFloat => Quantization.UnpackPosition(Py);

        // frob:doc docs/reference/hullbreach-net.md#gravitywellspawned
        public void Write(ref ByteWriter w)
        {
            w.WriteU8((byte)MessageKind.GravityWellSpawned);
            w.WriteI16(Px);
            w.WriteI16(Py);
            w.WriteI16(Mu);
            w.WriteU8(Radius);
            w.WriteU16(Seconds10);
        }

        // frob:doc docs/reference/hullbreach-net.md#gravitywellspawned
        public static GravityWellSpawned Read(ref ByteReader r)
        {
            r.ReadU8();
            short px = r.ReadI16();
            short py = r.ReadI16();
            short mu = r.ReadI16();
            byte radius = r.ReadU8();
            ushort seconds10 = r.ReadU16();
            return new GravityWellSpawned(px, py, mu, radius, seconds10);
        }
    }
}
