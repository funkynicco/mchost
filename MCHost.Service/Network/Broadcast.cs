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
            var data = _encoding.GetBytes($"IS {instanceId} {(int)status}|");
            Broadcast(data, 0, data.Length);
        }

        public void BroadcastInstanceLog(string instanceId, string text)
        {
            var data = _encoding.GetBytes($"IL {instanceId} {text.Replace("|", "&#124;")}|");
            Broadcast(data, 0, data.Length);
        }
    }
}
