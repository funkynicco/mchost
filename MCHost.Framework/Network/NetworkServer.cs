using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Network
{
    public abstract class NetworkServer<TClient> : IDisposable where TClient : BaseClient
    {
        private Socket _listenSocket;

        private readonly List<TClient> _clients = new List<TClient>();

        public NetworkServer()
        {
        }

        public void Dispose()
        {
            Stop();
        }

        protected abstract TClient AllocateClient(Socket socket);
        protected abstract void FreeClient(TClient client);

        public void Start(IEnumerable<IPEndPoint> points)
        {
            if (_listenSocket != null)
                throw new InvalidOperationException("Server already started.");

            _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                foreach (var point in points)
                    _listenSocket.Bind(point);

                _listenSocket.Listen(1024);
            }
            catch
            {
                _listenSocket.Dispose();
                _listenSocket = null;
                throw;
            }
        }

        public void Stop()
        {
            if (_listenSocket != null)
            {
                _listenSocket.Dispose();
                _listenSocket = null;
            }
        }

        public virtual void Process()
        {

        }
    }
}
