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

        public Socket Socket { get { return _socket; } }
        public bool IsDisconnect { get { return _disconnect; } }

        public BaseClient(Socket socket)
        {
            _socket = socket;
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
        }
    }
}
