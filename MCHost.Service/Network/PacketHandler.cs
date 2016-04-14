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
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"from client {client.Socket.RemoteEndPoint}: '{content}'");
            Console.ForegroundColor = ConsoleColor.Gray;

            client.Send($"echo '{content}'");
        }
    }
}
