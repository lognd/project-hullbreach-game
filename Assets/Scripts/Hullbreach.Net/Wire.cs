using System;

namespace Hullbreach.Net
{
    /// <summary>
    /// Little-endian cursor over a caller-owned byte buffer. No allocation
    /// per message: every message Write call takes a ByteWriter wrapping a
    /// buffer the caller already owns (a pooled send buffer, a stackalloc
    /// span, etc). Throws IndexOutOfRangeException on overflow rather than
    /// growing, because growing would mean allocating, which is exactly what
    /// this type exists to avoid.
    /// </summary>
    public struct ByteWriter
    {
        readonly byte[] _buffer;
        int _offset;

        /// <summary>Wraps `buffer`, writing from `offset` (default 0).</summary>
        public ByteWriter(byte[] buffer, int offset = 0)
        {
            _buffer = buffer;
            _offset = offset;
        }

        /// <summary>Bytes written so far (the offset into the buffer), i.e.
        /// the length of the message once writing is finished.</summary>
        public int Position => _offset;

        /// <summary>Writes one unsigned byte.</summary>
        public void WriteU8(byte v) => _buffer[_offset++] = v;

        /// <summary>Writes one signed byte (reinterpreted as its bit pattern).</summary>
        public void WriteI8(sbyte v) => _buffer[_offset++] = unchecked((byte)v);

        /// <summary>Writes a u16, low byte first.</summary>
        public void WriteU16(ushort v)
        {
            _buffer[_offset++] = (byte)(v & 0xFF);
            _buffer[_offset++] = (byte)((v >> 8) & 0xFF);
        }

        /// <summary>Writes an i16, low byte first.</summary>
        public void WriteI16(short v) => WriteU16(unchecked((ushort)v));

        /// <summary>Writes a u32, low byte first.</summary>
        public void WriteU32(uint v)
        {
            _buffer[_offset++] = (byte)(v & 0xFF);
            _buffer[_offset++] = (byte)((v >> 8) & 0xFF);
            _buffer[_offset++] = (byte)((v >> 16) & 0xFF);
            _buffer[_offset++] = (byte)((v >> 24) & 0xFF);
        }

        /// <summary>Writes an i32, low byte first.</summary>
        public void WriteI32(int v) => WriteU32(unchecked((uint)v));

        /// <summary>Writes an IEEE-754 f32, low byte first. Used only where
        /// the design explicitly allows a raw float; positions, velocities
        /// and angles go through Quantization instead.</summary>
        public void WriteF32(float v) => WriteU32((uint)BitConverter.SingleToInt32Bits(v));
    }

    /// <summary>
    /// Little-endian cursor for reading a byte buffer back out, the exact
    /// inverse of <see cref="ByteWriter"/>. Throws IndexOutOfRangeException
    /// on reading past the end, the same fail-fast behavior as the writer.
    /// </summary>
    public struct ByteReader
    {
        readonly byte[] _buffer;
        int _offset;

        /// <summary>Wraps `buffer`, reading from `offset` (default 0).</summary>
        public ByteReader(byte[] buffer, int offset = 0)
        {
            _buffer = buffer;
            _offset = offset;
        }

        /// <summary>Bytes consumed so far.</summary>
        public int Position => _offset;

        /// <summary>Reads one unsigned byte.</summary>
        public byte ReadU8() => _buffer[_offset++];

        /// <summary>Reads one signed byte.</summary>
        public sbyte ReadI8() => unchecked((sbyte)_buffer[_offset++]);

        /// <summary>Reads a u16, low byte first.</summary>
        public ushort ReadU16()
        {
            ushort v = (ushort)(_buffer[_offset] | (_buffer[_offset + 1] << 8));
            _offset += 2;
            return v;
        }

        /// <summary>Reads an i16, low byte first.</summary>
        public short ReadI16() => unchecked((short)ReadU16());

        /// <summary>Reads a u32, low byte first.</summary>
        public uint ReadU32()
        {
            uint v = (uint)(_buffer[_offset]
                | (_buffer[_offset + 1] << 8)
                | (_buffer[_offset + 2] << 16)
                | (_buffer[_offset + 3] << 24));
            _offset += 4;
            return v;
        }

        /// <summary>Reads an i32, low byte first.</summary>
        public int ReadI32() => unchecked((int)ReadU32());

        /// <summary>Reads an IEEE-754 f32, low byte first.</summary>
        public float ReadF32() => BitConverter.Int32BitsToSingle((int)ReadU32());
    }
}
