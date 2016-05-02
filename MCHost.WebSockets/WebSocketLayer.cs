using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.WebSockets
{
    public partial class WebSocketServer
    {
        public const long MaxWebSocketDataLength = 8388608; // 8 MB
        public const long MaxPayloadSize = 8388608; // 8 MB

        private void ProcessWebSocketData(WebSocketClient client, byte[] buffer, int offset, int length)
        {
            if (client.Buffer.Length + length > MaxWebSocketDataLength)
            {
                client.Disconnect("WebSocket buffer overflow");
                return;
            }

            client.Buffer.Offset = client.Buffer.Length;
            client.Buffer.Write(buffer, offset, length);

            var header = new WebSocketHeader();

            while (client.Buffer.Length > 0)
            {
                client.Buffer.Offset = 0;

                var result = header.Read(client.Buffer);
                if (result != WebSocketHeaderReadResult.Succeeded)
                {
                    if (result != WebSocketHeaderReadResult.NeedMoreData)
                        client.Disconnect("WebSocket protocol error");

                    break;
                }

                /*if (header.OpCode != WebSocketOpCode.Text &&
                    header.OpCode )
                {
                    Logger.Log(LogType.Warning, "Unsupported WebSocket OpCode: {0}", header.OpCode);
                    webClient.Disconnect();
                    break;
                }*/

                if (header.PayloadSize > MaxPayloadSize)
                {
                    client.Disconnect("Too big websocket packet received. Payload size: " + header.PayloadSize);
                    break;
                }

                if (!header.Masked)
                {
                    client.Disconnect("WebSocket clients must send masked data.");
                    break;
                }

                if (!header.Finished)
                {
                    client.Disconnect("Continuation frames not supported.");
                    break;
                }

                // parse
                if (client.Buffer.Offset + header.PayloadSize <= client.Buffer.Length) // check if we received all data
                {
                    if (header.Masked)
                    {
                        DecodeWebSocketData(
                            header.Mask,
                            client.Buffer.InternalBuffer,
                            client.Buffer.Offset,
                            (int)header.PayloadSize);
                    }

                    if (header.PayloadSize > 0 &&
                        header.OpCode == WebSocketOpCode.Text)
                    {
                        var str_data = Encoding.GetEncoding(1252).GetString(
                            client.Buffer.InternalBuffer,
                            client.Buffer.Offset,
                            (int)header.PayloadSize);

                        OnWebSocketData(
                            client,
                            str_data);
                    }

                    if (client.IsDisconnect)
                        break;

                    // delete from buffer
                    client.Buffer.Remove(header.HeaderSize + (int)header.PayloadSize);
                }
                else
                    return; // need more data
            }
        }

        protected virtual void OnWebSocketOpen(WebSocketClient client)
        {
        }

        protected virtual void OnWebSocketClosed(WebSocketClient client)
        {
        }

        protected virtual void OnWebSocketData(WebSocketClient client, string data)
        {
        }

        public static string GenerateWebSocketAcceptKey(string key) // TODO: make internal
        {
            key += "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"; // as per RFC, fixed guid

            using (var sha = SHA1.Create())
            {
                var sha_data = sha.ComputeHash(Encoding.GetEncoding(1252).GetBytes(key));
                return Convert.ToBase64String(sha_data);
            }
        }

        private static void DecodeWebSocketData(byte[] mask, byte[] buffer, int offset, int length)
        {
            for (int i = 0; i < length; ++i)
            {
                buffer[offset + i] = (byte)(buffer[offset + i] ^ mask[i % mask.Length]);
            }
        }
    }
}
