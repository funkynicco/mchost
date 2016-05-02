using MCHost.Framework;
using MCHost.Framework.Json;
using MCHost.Framework.Security;
using MCHost.WebSockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Service.Network
{
    public partial class WebSocketService
    {
        [WebSocketPacket("new", AccountRole.Operator)]
        private void OnNewInstance(WebSocketClient client, JsonObject json)
        {
            var packageName = json.GetMember<JsonString>("packageName").Value;

            _logger.Write(LogType.Notice, $"OnNewInstance {client.IP} => {packageName}");

            var package = _database.GetPackage(packageName);
            if (package == null)
            {
                client.SendPacket("err", new { message = "Package was not found." });
                return;
            }

            var instance = _instanceManager.CreateInstance(package);
            client.SendPacket("new", new
            {
                instanceId = instance.Id,
                packageName = package.Name
            });
            instance.Start();
        }
    }
}
