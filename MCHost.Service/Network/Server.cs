using MCHost.Framework.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Service.Network
{
    public class ServerClient : BaseClient
    {
        public ServerClient(Socket socket) :
            base(socket)
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {

            }

            base.Dispose(disposing);
        }
    }

    public class Server : NetworkServer<ServerClient>
    {
        protected override ServerClient AllocateClient(Socket socket)
        {
            return new ServerClient(socket);
        }

        protected override void FreeClient(ServerClient client)
        {
            client.Dispose();
        }
    }
}
