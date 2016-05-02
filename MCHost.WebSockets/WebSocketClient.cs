using MCHost.Framework.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.WebSockets
{
    public partial class WebSocketClient : BaseClient
    {
        public DataBuffer Buffer { get; private set; }
        public DateTime LastDataReceived { get; set; }
        public bool IsWebSocket { get; private set; }
        public DateTime LastSendWebSocketPing { get; set; }

        public object Tag { get; set; }

        private string _userIP = null; // if reverse proxy then we set this and below IP will return _userIP instead

        public override string IP
        {
            get
            {
                if (_userIP != null)
                    return _userIP;

                return base.IP;
            }
        }

        public WebSocketClient(Socket socket) :
            base(socket)
        {
            Buffer = new DataBuffer();
            LastDataReceived = DateTime.UtcNow;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {

            }

            base.Dispose(disposing);
        }

        public void SetUserAddress(string ip)
        {
            _userIP = ip;
        }

        public void UpgradeToWebSocket()
        {
            IsWebSocket = true;
        }

        private void Send(byte[] buffer, int offset, int length)
        {
            try
            {
                while (offset < length)
                {
                    offset += Socket.Send(buffer, offset, length - offset, SocketFlags.None);
                }
            }
            catch
            {
                Disconnect();
                throw;
            }
        }

        public void Send(DataBuffer buffer)
        {
            Send(buffer.InternalBuffer, 0, buffer.Length);
        }

        public void SendErrorResponse(int code, string code_error, string message)
        {
            var str = "<!DOCTYPE html><html><head><title>Error</title></head><body><h2>" + message + "</h2></body></html>";

            var pack = string.Format(
                "HTTP/1.1 {0} {1}\r\nContent-Type: text/html\r\nContent-Length: {2}\r\n\r\n{3}",
                code,
                code_error,
                str.Length,
                str);

            var data = Encoding.GetEncoding(1252).GetBytes(pack);
            Send(data, 0, data.Length);
        }

        public void SendWebSocketPing()
        {
            var data = new byte[2] { 0x89, 0 };
            Send(data, 0, data.Length);
        }

        public void SendWebSocketText(string text)
        {
            var data = Encoding.GetEncoding(1252).GetBytes(text);

            var buffer = new DataBuffer();
            buffer.Write((byte)0x81); // text frame and FIN

            if (data.Length <= 125)
            {
                buffer.Write((byte)data.Length);
            }
            else if (data.Length <= 65535)
            {
                buffer.Write((byte)126);
                buffer.Write(WebSocketHeader.ReverseBytes((ushort)data.Length));
            }
            else
            {
                buffer.Write((byte)127);
                buffer.Write((long)WebSocketHeader.ReverseBytes((ulong)data.Length));
            }

            buffer.Write(data, 0, data.Length);

            Send(buffer);
        }
    }
}
