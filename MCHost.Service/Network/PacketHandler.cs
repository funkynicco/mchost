using MCHost.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Service.Network
{
    public partial class Server
    {
        [Packet]
        void OnRequestInstances(ServerClient client, string content)
        {
            _logger.Write(LogType.Success, $"from client {client.Socket.RemoteEndPoint}: '{content}'");

            client.Send($"echo '{content}'");
        }
    }
}
