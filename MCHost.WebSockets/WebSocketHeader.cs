using MCHost.Framework.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.WebSockets
{
    internal class WebSocketHeader
    {
        public int HeaderSize { get; private set; }
        public long PayloadSize { get; private set; }
        public bool Finished { get; private set; }
        public bool Masked { get; private set; }
        public WebSocketOpCode OpCode { get; private set; }
        public byte[] Mask { get; private set; }

        public WebSocketHeaderReadResult Read(DataBuffer buffer)
        {
            if (buffer.Offset + 2 > buffer.Length)
                return WebSocketHeaderReadResult.NeedMoreData;

            var data = new byte[2];
            buffer.ReadBytes(data, 0, 2);
            Finished = (data[0] & 0x80) != 0;
            OpCode = (WebSocketOpCode)(data[0] & 0x0f);
            Masked = (data[1] & 0x80) != 0;

            byte stream_size = (byte)(data[1] & 0x7f);

            if (stream_size <= 125)
            {
                // small stream
                HeaderSize = 6;
                PayloadSize = stream_size;
            }
            else if (stream_size == 126)
            {
                // medium stream
                if (buffer.Offset + 2 > buffer.Length)
                    return WebSocketHeaderReadResult.NeedMoreData;

                HeaderSize = 8;
                PayloadSize = ReverseBytes((ushort)buffer.ReadInt16());
            }
            else if (stream_size == 127)
            {
                // big stream
                if (buffer.Offset + 8 > buffer.Length)
                    return WebSocketHeaderReadResult.NeedMoreData;

                var value = (ulong)buffer.ReadInt64();
                if (value < 0L ||
                    value > long.MaxValue)
                    return WebSocketHeaderReadResult.ProtocolError;

                HeaderSize = 14;
                PayloadSize = (long)ReverseBytes(value);
            }

            if (buffer.Offset + 4 > buffer.Length)
                return WebSocketHeaderReadResult.NeedMoreData;

            Mask = new byte[4];
            buffer.ReadBytes(Mask, 0, 4);

            return WebSocketHeaderReadResult.Succeeded;
        }

        internal static ushort ReverseBytes(ushort value)
        {
            return Convert.ToUInt16(
                (value >> 8) |
                (value & 0xff) << 8);
        }

        internal static ulong ReverseBytes(ulong value)
        {
            return
                ((value >> 56) & 0xff) |
                (((value >> 48) & 0xff) << 8) |
                (((value >> 40) & 0xff) << 16) |
                (((value >> 32) & 0xff) << 24) |
                (((value >> 24) & 0xff) << 32) |
                (((value >> 16) & 0xff) << 40) |
                (((value >> 8) & 0xff) << 48) |
                (((value) & 0xff) << 56);
        }
    }

    internal enum WebSocketHeaderReadResult
    {
        Succeeded,
        NeedMoreData,
        ProtocolError
    }

    internal enum WebSocketOpCode : byte
    {
        Continuation = 0x00,
        Text = 0x01,
        Binary = 0x02,
        Close = 0x08,
        Ping = 0x09,
        Pong = 0x0A
    }
}
