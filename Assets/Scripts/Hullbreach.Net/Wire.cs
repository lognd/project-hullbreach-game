using System;

namespace Hullbreach.Net
{
    // No allocation per message: every Write call takes a ByteWriter wrapping
    // a buffer the caller already owns. Throws IndexOutOfRangeException on
    // overflow rather than growing, since growing would allocate.
    // frob:doc docs/reference/hullbreach-net.md#bytewriter
    public struct ByteWriter
    {
        readonly byte[] _buffer;
        int _offset;

        // frob:doc docs/reference/hullbreach-net.md#bytewriter
        public ByteWriter(byte[] buffer, int offset = 0)
        {
            _buffer = buffer;
            _offset = offset;
        }

        // frob:doc docs/reference/hullbreach-net.md#bytewriter
        public int Position => _offset;

        // frob:doc docs/reference/hullbreach-net.md#bytewriter
        public void WriteU8(byte v) => _buffer[_offset++] = v;

        // frob:doc docs/reference/hullbreach-net.md#bytewriter
        public void WriteI8(sbyte v) => _buffer[_offset++] = unchecked((byte)v);

        // frob:doc docs/reference/hullbreach-net.md#bytewriter
        public void WriteU16(ushort v)
        {
            _buffer[_offset++] = (byte)(v & 0xFF);
            _buffer[_offset++] = (byte)((v >> 8) & 0xFF);
        }

        // frob:doc docs/reference/hullbreach-net.md#bytewriter
        public void WriteI16(short v) => WriteU16(unchecked((ushort)v));

        // frob:doc docs/reference/hullbreach-net.md#bytewriter
        public void WriteU32(uint v)
        {
            _buffer[_offset++] = (byte)(v & 0xFF);
            _buffer[_offset++] = (byte)((v >> 8) & 0xFF);
            _buffer[_offset++] = (byte)((v >> 16) & 0xFF);
            _buffer[_offset++] = (byte)((v >> 24) & 0xFF);
        }

        // frob:doc docs/reference/hullbreach-net.md#bytewriter
        public void WriteI32(int v) => WriteU32(unchecked((uint)v));

        // Positions, velocities and angles go through Quantization instead;
        // this is only for the rare field the design explicitly allows raw.
        // frob:doc docs/reference/hullbreach-net.md#bytewriter
        public void WriteF32(float v) => WriteU32((uint)BitConverter.SingleToInt32Bits(v));
    }

    // The exact inverse of ByteWriter; same fail-fast behavior on overrun.
    // frob:doc docs/reference/hullbreach-net.md#bytereader
    public struct ByteReader
    {
        readonly byte[] _buffer;
        int _offset;

        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public ByteReader(byte[] buffer, int offset = 0)
        {
            _buffer = buffer;
            _offset = offset;
        }

        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public int Position => _offset;

        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public byte ReadU8() => _buffer[_offset++];

        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public sbyte ReadI8() => unchecked((sbyte)_buffer[_offset++]);

        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public ushort ReadU16()
        {
            ushort v = (ushort)(_buffer[_offset] | (_buffer[_offset + 1] << 8));
            _offset += 2;
            return v;
        }

        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public short ReadI16() => unchecked((short)ReadU16());

        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public uint ReadU32()
        {
            uint v = (uint)(_buffer[_offset]
                | (_buffer[_offset + 1] << 8)
                | (_buffer[_offset + 2] << 16)
                | (_buffer[_offset + 3] << 24));
            _offset += 4;
            return v;
        }

        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public int ReadI32() => unchecked((int)ReadU32());

        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public float ReadF32() => BitConverter.Int32BitsToSingle((int)ReadU32());
    }
}
