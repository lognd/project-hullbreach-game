using System;

namespace Hullbreach.Net
{
    /// <summary>
    /// Wire formats.
    ///
    /// THE GOVERNING RULE: send CAUSES, never EFFECTS.
    ///
    /// Integer flood fill is bit-deterministic on every platform; the float FE
    /// solve is not (SIMD width, FMA contraction, iteration counts all
    /// diverge). So the server sends "block (x,y) died" and BOTH sides
    /// independently derive which components detached. A hit that splits a
    /// 10,000-block ship in half puts 4 bytes on the wire, not 5,000 blocks.
    ///
    /// The FE result never goes on the wire at all. Clients compute their own
    /// purely for the S37 tint, where divergence is cosmetic and invisible.
    ///
    /// Every message is prefixed by a <see cref="MessageKind"/> byte. Reliable
    /// ordered messages additionally carry a u32 sequence number right after
    /// the kind byte, so a receiver can buffer and reorder without touching
    /// the payload layout below.
    /// </summary>
    public static class NetMessages
    {
        // See ByteWriter/ByteReader (Wire.cs) for the little-endian primitive
        // encoding, ITransport for the two delivery channels, ServerSimulation
        // for the emitting side, and ClientReplica for the consuming side.
    }

    /// <summary>
    /// One byte identifying which message struct follows in a buffer. Kept
    /// as its own byte (not folded into a discriminated union) so a receiver
    /// can dispatch with a single switch before deserializing anything.
    /// </summary>
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

    /// <summary>
    /// Client -> server, unreliable, one per tick: this tick's player intent.
    /// Latest-wins on the server (ServerSimulation keeps only the newest
    /// input per peer), so dropping one is harmless.
    ///
    /// Layout (8 bytes): kind(1) netId(2) tick(4) thrustAxis(1) steer(1) flags(1).
    /// </summary>
    public readonly struct InputMessage
    {
        public readonly ushort NetId;
        public readonly uint Tick;

        /// <summary>Quantized -127..127, unpacked to -1f..1f by /127f.</summary>
        public readonly sbyte ThrustAxis;

        /// <summary>Quantized -127..127, unpacked to -1f..1f by /127f.</summary>
        public readonly sbyte Steer;

        /// <summary>Bit0 = fire pressed. Other bits reserved.</summary>
        public readonly byte Flags;

        public const byte FireBit = 0x01;

        public InputMessage(ushort netId, uint tick, sbyte thrustAxis, sbyte steer, byte flags)
        {
            NetId = netId;
            Tick = tick;
            ThrustAxis = thrustAxis;
            Steer = steer;
            Flags = flags;
        }

        /// <summary>Builds an InputMessage from float intent (-1..1), quantizing
        /// each axis to the nearest representable sbyte.</summary>
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

        /// <summary>ThrustAxis unpacked back to -1f..1f.</summary>
        public float ThrustAxisFloat => ThrustAxis / 127f;

        /// <summary>Steer unpacked back to -1f..1f.</summary>
        public float SteerFloat => Steer / 127f;

        /// <summary>Whether the fire bit is set.</summary>
        public bool FirePressed => (Flags & FireBit) != 0;

        /// <summary>Writes this message, including its MessageKind prefix.</summary>
        public void Write(ref ByteWriter w)
        {
            w.WriteU8((byte)MessageKind.Input);
            w.WriteU16(NetId);
            w.WriteU32(Tick);
            w.WriteI8(ThrustAxis);
            w.WriteI8(Steer);
            w.WriteU8(Flags);
        }

        /// <summary>Reads an InputMessage; assumes the MessageKind byte has
        /// already been consumed (or not yet, per Read's convention below:
        /// this overload reads it FOR you, mirroring Write).</summary>
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

    /// <summary>One block as carried by <see cref="ShipSnapshot"/>: position
    /// (grid-local, fits an sbyte per BlockKey's -128..127 range), type,
    /// modifiers and accumulated damage.</summary>
    public readonly struct SnapshotBlock
    {
        public readonly sbyte X;
        public readonly sbyte Y;
        public readonly byte TypeId;
        public readonly byte Mods;
        public readonly byte Damage;

        public SnapshotBlock(sbyte x, sbyte y, byte typeId, byte mods, byte damage)
        {
            X = x;
            Y = y;
            TypeId = typeId;
            Mods = mods;
            Damage = damage;
        }
    }

    /// <summary>
    /// Server -> client, RELIABLE, sent once on join or respawn: the full
    /// block layout plus the ship's current pose/velocity. 5 bytes per block
    /// raw; block grids deflate ~10:1 under a reliable transport's own
    /// compression, so a 10k-block ship is a few KB. Fine as a one-off; never
    /// sent per tick.
    ///
    /// Layout: kind(1) seq(4) netId(2) count(2) blocks(5*count)
    ///         px(2) py(2) rot(2) vx(2) vy(2) av(2)   [quantized, see ShipState]
    /// </summary>
    public readonly struct ShipSnapshot
    {
        public readonly uint Sequence;
        public readonly ushort NetId;
        public readonly SnapshotBlock[] Blocks;
        public readonly short Px;
        public readonly short Py;
        public readonly ushort Rot;
        public readonly short Vx;
        public readonly short Vy;
        public readonly ushort Av;

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

        /// <summary>Total encoded size in bytes for this snapshot's block count,
        /// so a caller can size its send buffer without writing twice.</summary>
        public int ByteSize => 1 + 4 + 2 + 2 + (Blocks.Length * 5) + 2 + 2 + 2 + 2 + 2 + 2;

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

    /// <summary>
    /// Server -> client, UNRELIABLE, ~30-50 Hz: this tick's pose. 17 bytes on
    /// the wire (kind + netId + 6 quantized fields), so two ships cost well
    /// under 1 KB/s. Position/velocity quantized at 1/256 world unit
    /// (Quantization.PackPosition), angle as a 16-bit turn fraction
    /// (Quantization.PackAngle); losing one is harmless since the next one
    /// supersedes it.
    /// </summary>
    public readonly struct ShipState
    {
        public readonly ushort NetId;
        public readonly short Px;
        public readonly short Py;
        public readonly ushort Rot;
        public readonly short Vx;
        public readonly short Vy;
        public readonly ushort Av;

        /// <summary>Encoded size in bytes: 1 kind + 2 netId + 6*2 fields = 15.</summary>
        public const int ByteSize = 1 + 2 + 2 + 2 + 2 + 2 + 2 + 2;

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

    /// <summary>
    /// Server -> client, RELIABLE ORDERED: a block was placed. Carries a
    /// sequence number because placement order matters for which cell wins a
    /// race, exactly like destruction order matters for FindDetached.
    /// Layout: kind(1) seq(4) netId(2) x(1) y(1) typeId(1) mods(1) = 11 bytes.
    /// </summary>
    public readonly struct BlockPlaced
    {
        public readonly uint Sequence;
        public readonly ushort NetId;
        public readonly sbyte X;
        public readonly sbyte Y;
        public readonly byte TypeId;
        public readonly byte Mods;

        public BlockPlaced(uint sequence, ushort netId, sbyte x, sbyte y, byte typeId, byte mods)
        {
            Sequence = sequence;
            NetId = netId;
            X = x;
            Y = y;
            TypeId = typeId;
            Mods = mods;
        }

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

    /// <summary>
    /// Server -> client, RELIABLE ORDERED: a block died. This is the whole
    /// point of the design: both sides run Connectivity.FindDetached after
    /// applying this, and derive the identical set of fragments without a
    /// block list ever crossing the wire. Layout: kind(1) seq(4) netId(2)
    /// x(1) y(1) = 9 bytes.
    /// </summary>
    public readonly struct BlockDestroyed
    {
        public readonly uint Sequence;
        public readonly ushort NetId;
        public readonly sbyte X;
        public readonly sbyte Y;

        public BlockDestroyed(uint sequence, ushort netId, sbyte x, sbyte y)
        {
            Sequence = sequence;
            NetId = netId;
            X = x;
            Y = y;
        }

        public void Write(ref ByteWriter w)
        {
            w.WriteU8((byte)MessageKind.BlockDestroyed);
            w.WriteU32(Sequence);
            w.WriteU16(NetId);
            w.WriteI8(X);
            w.WriteI8(Y);
        }

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

    /// <summary>
    /// Server -> client, RELIABLE ORDERED: a detached component (from a
    /// FindDetached split, on the tick's BlockDestroyed/buckling events) is
    /// spawned as its own body. Carries NO block list: both sides already
    /// know which blocks left, because they ran the same flood fill after
    /// applying the same ordered destruction events first. Layout: kind(1)
    /// seq(4) parentId(2) newId(2) px(2) py(2) rot(2) vx(2) vy(2) av(2)
    /// = 21 bytes.
    /// </summary>
    public readonly struct FragmentSpawned
    {
        public readonly uint Sequence;
        public readonly ushort ParentId;
        public readonly ushort NewId;
        public readonly short Px;
        public readonly short Py;
        public readonly ushort Rot;
        public readonly short Vx;
        public readonly short Vy;
        public readonly ushort Av;

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

    /// <summary>
    /// Server -> client, RELIABLE ORDERED: a block took damage but did not
    /// die. Damage is a CAUSE the client cannot derive on its own (the FE
    /// solve that computed it is not bit-identical across machines), so
    /// unlike destruction it must be sent explicitly rather than recomputed
    /// locally. Layout: kind(1) seq(4) netId(2) x(1) y(1) damage(1) = 10 bytes.
    /// </summary>
    public readonly struct BlockDamaged
    {
        public readonly uint Sequence;
        public readonly ushort NetId;
        public readonly sbyte X;
        public readonly sbyte Y;
        public readonly byte Damage;

        public BlockDamaged(uint sequence, ushort netId, sbyte x, sbyte y, byte damage)
        {
            Sequence = sequence;
            NetId = netId;
            X = x;
            Y = y;
            Damage = damage;
        }

        public void Write(ref ByteWriter w)
        {
            w.WriteU8((byte)MessageKind.BlockDamaged);
            w.WriteU32(Sequence);
            w.WriteU16(NetId);
            w.WriteI8(X);
            w.WriteI8(Y);
            w.WriteU8(Damage);
        }

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

    /// <summary>
    /// Server -> client, RELIABLE ORDERED: a temporary variant transform
    /// (ShipBody.ApplyPowerup) landed on a block. Layout: kind(1) seq(4)
    /// netId(2) x(1) y(1) variant(1) seconds10(2) = 12 bytes. Seconds are
    /// sent as tenths of a second in a u16 (seconds10 = seconds*10) so a
    /// multi-minute buff still fits without a float on the wire.
    /// </summary>
    public readonly struct PowerupApplied
    {
        public readonly uint Sequence;
        public readonly ushort NetId;
        public readonly sbyte X;
        public readonly sbyte Y;
        public readonly byte Variant;
        public readonly ushort Seconds10;

        public PowerupApplied(uint sequence, ushort netId, sbyte x, sbyte y, byte variant, ushort seconds10)
        {
            Sequence = sequence;
            NetId = netId;
            X = x;
            Y = y;
            Variant = variant;
            Seconds10 = seconds10;
        }

        /// <summary>Seconds10 unpacked back to seconds.</summary>
        public float Seconds => Seconds10 / 10f;

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

    /// <summary>
    /// Server -> client, RELIABLE ORDERED: a temporary gravity well/anti-well
    /// (IWorldSink.AddTemporaryGravity) was dropped into the world field.
    /// Layout: kind(1) seq(4) px(2) py(2) mu(2) radius(1) seconds10(2)
    /// = 14 bytes. Mu is quantized as a signed 16-bit fixed point at 1/16
    /// per unit (i.e. Mu/16f), wide enough for both attraction and the
    /// anti-gravity gun's negative Mu, and radius is a single unsigned byte
    /// since well radii never need sub-unit precision.
    /// </summary>
    public readonly struct GravityWellSpawned
    {
        public readonly short Px;
        public readonly short Py;
        public readonly short Mu;
        public readonly byte Radius;
        public readonly ushort Seconds10;

        /// <summary>Fixed-point scale for Mu: one unit of Mu16 is 1/16 of a
        /// world Mu unit, giving a range of roughly +-2048 with 1/16
        /// resolution, comfortably covering both gravity-gun wells and
        /// anti-gravity anti-wells.</summary>
        public const float MuScale = 16f;

        public GravityWellSpawned(short px, short py, short mu, byte radius, ushort seconds10)
        {
            Px = px;
            Py = py;
            Mu = mu;
            Radius = radius;
            Seconds10 = seconds10;
        }

        /// <summary>Builds from float world units, quantizing Mu and rounding
        /// radius/seconds into their wire representations.</summary>
        public static GravityWellSpawned FromFloats(float px, float py, float mu, float radius, float seconds)
        {
            short qmu = (short)Math.Round(Math.Max(short.MinValue, Math.Min(short.MaxValue, mu * MuScale)), MidpointRounding.AwayFromZero);
            byte qradius = (byte)Math.Max(0, Math.Min(255, Math.Round(radius, MidpointRounding.AwayFromZero)));
            ushort qseconds10 = (ushort)Math.Max(0, Math.Min(ushort.MaxValue, Math.Round(seconds * 10f, MidpointRounding.AwayFromZero)));
            return new GravityWellSpawned(Quantization.PackPosition(px), Quantization.PackPosition(py), qmu, qradius, qseconds10);
        }

        public float MuFloat => Mu / MuScale;
        public float RadiusFloat => Radius;
        public float SecondsFloat => Seconds10 / 10f;
        public float PxFloat => Quantization.UnpackPosition(Px);
        public float PyFloat => Quantization.UnpackPosition(Py);

        public void Write(ref ByteWriter w)
        {
            w.WriteU8((byte)MessageKind.GravityWellSpawned);
            w.WriteI16(Px);
            w.WriteI16(Py);
            w.WriteI16(Mu);
            w.WriteU8(Radius);
            w.WriteU16(Seconds10);
        }

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
