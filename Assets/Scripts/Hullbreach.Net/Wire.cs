using System;

namespace Hullbreach.Net
{
    // No allocation per message; see the reference page for why.
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

    // The inverse of ByteWriter, bounded by the received length: an overrun
    // never throws or reads stale bytes, it sets Failed and yields zeros, so
    // a decoder checks Failed once after reading (INV-001).
    // frob:doc docs/reference/hullbreach-net.md#bytereader
    // frob:invariant INV-001
    public struct ByteReader
    {
        readonly byte[] _buffer;
        readonly int _end;
        int _offset;
        bool _failed;

        // `length` is the count of valid bytes from `offset` (a transport's
        // received length); -1 means the rest of the buffer.
        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public ByteReader(byte[] buffer, int offset = 0, int length = -1)
        {
            _buffer = buffer;
            _offset = offset;
            int end = length < 0 ? buffer.Length : offset + length;
            _end = end > buffer.Length ? buffer.Length : end;
            _failed = offset < 0 || offset > _end;
        }

        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public int Position => _offset;

        // Bytes left to read; 0 once the reader has failed.
        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public int Remaining => _failed ? 0 : _end - _offset;

        // True once any read ran past the end or Fail() was called.
        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public bool Failed => _failed;

        // Marks the message malformed (for example an implausible count).
        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public void Fail() => _failed = true;

        // Reserves `count` bytes; on shortfall marks the reader failed.
        bool TryRead(int count)
        {
            if (_failed || count > _end - _offset)
            {
                _failed = true;
                return false;
            }
            return true;
        }

        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public byte ReadU8() => TryRead(1) ? _buffer[_offset++] : (byte)0;

        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public sbyte ReadI8() => unchecked((sbyte)ReadU8());

        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public ushort ReadU16()
        {
            if (!TryRead(2)) return 0;
            ushort v = (ushort)(_buffer[_offset] | (_buffer[_offset + 1] << 8));
            _offset += 2;
            return v;
        }

        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public short ReadI16() => unchecked((short)ReadU16());

        // frob:doc docs/reference/hullbreach-net.md#bytereader
        public uint ReadU32()
        {
            if (!TryRead(4)) return 0;
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
