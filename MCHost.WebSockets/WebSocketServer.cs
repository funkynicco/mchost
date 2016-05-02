using MCHost.Framework.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.WebSockets
{
    public partial class WebSocketServer : NetworkServer<WebSocketClient>
    {
        private readonly bool _isBackendServer;

        public WebSocketServer(bool isBackendServer)
        {
            _isBackendServer = isBackendServer;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {

            }

            base.Dispose(disposing);
        }

        protected override WebSocketClient AllocateClient(Socket socket)
        {
            return new WebSocketClient(socket);
        }

        protected override void FreeClient(WebSocketClient client)
        {
            client.Dispose();
        }

        protected override void OnClientConnected(WebSocketClient client)
        {
        }

        protected override void OnClientDisconnected(WebSocketClient client)
        {
            // use disconnectmessage etc..
            if (client.IsWebSocket)
                OnWebSocketClosed(client);
        }

        protected override void OnClientData(WebSocketClient client, byte[] buffer, int offset, int length)
        {
            if (client.IsWebSocket)
                ProcessWebSocketData(client, buffer, offset, length);
            else
                ProcessHttpData(client, buffer, offset, length);
        }

        public override void Process()
        {
            base.Process();

            // implement websocket ping
        }
    }
}
