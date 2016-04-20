using MCHost.Framework.Minecraft;
using MCHost.Framework.Network;
using MCHost.Service.Minecraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Service.Network
{
    public partial class Server
    {
        public void BroadcastInstanceStatus(string instanceId, InstanceStatus status)
        {
            var packet = new Packet(Header.InstanceStatus, Packet.NoRequestId);
            packet.Write(instanceId);
            packet.Write((int)status);

            int length;
            var buffer = packet.GetInternalBuffer(out length);
            Broadcast(buffer, 0, length);
        }

        public void BroadcastInstanceLog(string instanceId, string text)
        {
            var packet = new Packet(Header.InstanceLog, Packet.NoRequestId);
            packet.Write(instanceId);
            packet.Write(text);

            int length;
            var buffer = packet.GetInternalBuffer(out length);
            Broadcast(buffer, 0, length);
        }
    }
}
