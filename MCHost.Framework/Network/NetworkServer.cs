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
        private readonly byte[] _buffer = new byte[65536];

        protected IEnumerable<TClient> Clients { get { return _clients; } }

        private readonly object _lock = new object();

        protected NetworkServer()
        {
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Stop();
            }
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

        public virtual void Broadcast(byte[] buffer, int offset, int length)
        {
            lock (_lock)
            {
                foreach (var client in _clients)
                {
                    if (!client.IsDisconnect)
                    {
                        var pos = 0;
                        while (pos < length)
                        {
                            try
                            {
                                var sent = client.Socket.Send(buffer, offset, length, SocketFlags.None);
                                if (sent <= 0)
                                    throw new Exception("Sent 0 or less bytes.");

                                pos += sent;

                            }
                            catch
                            {
                                client.Disconnect();
                                break;
                            }
                        }
                    }
                }
            }
        }

        public virtual void Process()
        {
            if (_listenSocket != null)
            {
                bool poll;
                Socket socket;

                try { poll = _listenSocket.Poll(1, SelectMode.SelectRead); }
                catch { poll = false; }

                if (poll)
                {
                    try { socket = _listenSocket.Accept(); }
                    catch { socket = null; }

                    if (socket != null)
                    {
                        lock (_lock)
                        {
                            var client = AllocateClient(socket);
                            _clients.Add(client);
                            OnClientConnected(client);
                        }
                    }
                }

                lock (_lock)
                {
                    for (int i = 0; i < _clients.Count;)
                    {
                        var client = _clients[i];

                        int len = 0;
                        if (!client.IsDisconnect) // len is 0 by default so it will be disconnected if IsDisconnect is true
                        {
                            try { poll = client.Socket.Poll(1, SelectMode.SelectRead); }
                            catch { poll = false; client.Disconnect(); }

                            if (!poll)
                            {
                                ++i;
                                continue;
                            }

                            try
                            {
                                len = client.Socket.Receive(_buffer);
                            }
                            catch
                            {
                                len = 0;
                            }
                        }

                        if (len > 0)
                        {
                            OnClientData(client, _buffer, 0, len);
                        }
                        else
                        {
                            OnClientDisconnected(client);

                            _clients.RemoveAt(i);
                            FreeClient(client);
                            continue;
                        }

                        ++i;
                    }
                }
            }
        }

        protected virtual void OnClientConnected(TClient client)
        {
        }

        protected virtual void OnClientDisconnected(TClient client)
        {
        }

        protected virtual void OnClientData(TClient client, byte[] buffer, int offset, int length)
        {
        }
    }
}