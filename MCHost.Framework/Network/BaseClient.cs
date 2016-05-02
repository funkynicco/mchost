using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Network
{
    public abstract class BaseClient : IDisposable
    {
        private Socket _socket;
        private bool _disconnect = false;
        private string _disconnectMessage = null;

        public Socket Socket { get { return _socket; } }
        public bool IsDisconnect { get { return _disconnect; } }
        public string DisconnectMessage { get { return _disconnectMessage; } }

        private readonly string _ip;
        public virtual string IP { get { return _ip; } }

        public BaseClient(Socket socket)
        {
            _socket = socket;
            _ip = socket.RemoteEndPoint.ToString().Split(':')[0];
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_socket != null)
                {
                    _socket.Dispose();
                    _socket = null;
                }
            }
        }

        public void Disconnect()
        {
            _disconnect = true;
            _disconnectMessage = null;
        }

        public void Disconnect(string message)
        {
            _disconnect = true;
            _disconnectMessage = message;
        }
    }
}
