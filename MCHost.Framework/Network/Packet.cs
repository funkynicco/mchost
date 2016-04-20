using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Network
{
    public class Packet : IDataWriter
    {
        public const int NoRequestId = 0;
        public const int HeaderSize = 12;

        private readonly DataBuffer _buffer = new DataBuffer();
        private readonly Header _header;

        public Packet(Header header, int requestId)
        {
            _header = header;

            _buffer.Write((int)header);
            _buffer.Write(requestId);
            _buffer.Write(0); // packet size
        }

        public void WriteTo(DataBuffer buffer)
        {
            buffer.Write(_buffer.InternalBuffer, 0, _buffer.Length);
        }

        public byte[] GetInternalBuffer(out int length)
        {
            length = _buffer.Length;
            return _buffer.InternalBuffer;
        }

        private void UpdatePacketLength()
        {
            var offset = _buffer.Offset;
            _buffer.Offset = 8; // packet size
            _buffer.Write(_buffer.Length - HeaderSize);
            _buffer.Offset = offset;
        }

        public void Write(byte[] buffer, int offset, int length)
        {
            _buffer.Write(buffer, offset, length);
            UpdatePacketLength();
        }

        public void Write(byte value)
        {
            _buffer.Write(value);
            UpdatePacketLength();
        }

        public void Write(bool value)
        {
            _buffer.Write(value);
            UpdatePacketLength();
        }

        public void Write(short value)
        {
            _buffer.Write(value);
            UpdatePacketLength();
        }

        public void Write(int value)
        {
            _buffer.Write(value);
            UpdatePacketLength();
        }

        public void Write(long value)
        {
            _buffer.Write(value);
            UpdatePacketLength();
        }

        public void Write(string value)
        {
            _buffer.Write(value);
            UpdatePacketLength();
        }
    }

    public enum Header : int
    {
        Ping = 1,
        New,
        InstanceStatus,
        InstanceLog,
        InstanceConfiguration,
        List,
        Error,

        Command,
        Terminate,

        MaxHeader
    }
}
