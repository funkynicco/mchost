using MCHost.Framework;
using MCHost.Framework.Json;
using MCHost.Framework.Security;
using MCHost.Service.Minecraft;
using MCHost.WebSockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MCHost.Framework.Minecraft;
using System.Net.Sockets;

namespace MCHost.Service.Network
{
    public partial class WebSocketService : IInstanceEventListener
    {
        private Queue<Action> _mainLoopDelegates = new Queue<Action>();
        private readonly object _mainLoopQueueLock = new object();

        protected void ExecuteOnMainThread(Action action)
        {
            lock (_mainLoopQueueLock)
            {
                _mainLoopDelegates.Enqueue(action);
            }
        }

        public void ProcessDelegates()
        {
            Queue<Action> delegates;
            lock (_mainLoopQueueLock)
            {
                if (_mainLoopDelegates.Count == 0)
                    return;

                delegates = _mainLoopDelegates;
                _mainLoopDelegates = new Queue<Action>();
            }

            foreach (var action in delegates)
            {
                action();
            }
        }

        public void OnInstanceStatus(string instanceId, InstanceStatus status)
        {
            ExecuteOnMainThread(() =>
            {
                var data = new
                {
                    instanceId = instanceId,
                    status = (int)status
                };

                foreach (var client in Clients)
                {
                    try
                    {
                        client.SendPacket("is", data);
                    }
                    catch (SocketException)
                    {
                    }
                }
            });
        }

        public void OnInstanceLog(string instanceId, DateTime time, string text)
        {
            ExecuteOnMainThread(() =>
                {
                    foreach (var client in Clients)
                    {
                        try
                        {
                            client.SendPacket("il", new
                            {
                                instanceId = instanceId,
                                text = "[" + (client.Tag as WebSocketTag).User.GetLocalDateTime(time).ToString("HH:mm:ss") + "] " + text
                            });
                        }
                        catch (SocketException)
                        {
                        }
                    }
                });
        }

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
            Broadcast("new", new
            {
                instanceId = instance.Id,
                packageName = package.Name,
                status = instance.Status,
                address = _configuration.MinecraftHostname + ":" + instance.BindingInterface.Port
            });
            instance.Start();
        }

        [WebSocketPacket("cmd", AccountRole.Operator)]
        private void OnConsoleCommand(WebSocketClient client, JsonObject json)
        {
            var instanceId = json.GetMember<JsonString>("instanceId").Value;
            var command = json.GetMember<JsonString>("command").Value;

            _logger.Write(LogType.Notice, $"({instanceId}) {command}");

            if (!_instanceManager.PostCommand(instanceId, command))
            {
                _logger.Write(LogType.Warning, "Could not post command.");
                client.SendPacket("err", new { message = "Could not post command." });
            }
            else
            {
                var cmdline = (client.Tag as WebSocketTag).User.DisplayName + "> " + command;
                _logger.Write(LogType.Notice, $"[{instanceId}] {cmdline}");

                InstanceLogManager.AddLog(instanceId, cmdline);
                Broadcast("il", new
                {
                    instanceId = instanceId,
                    text = "[" + (client.Tag as WebSocketTag).User.GetLocalDateTime(DateTime.UtcNow).ToString("HH:mm:ss") + "] " + cmdline
                });
            }
        }

        [WebSocketPacket("stp", AccountRole.Operator)]
        private void OnShutdownInstance(WebSocketClient client, JsonObject json)
        {
            var instanceId = json.GetMember<JsonString>("instanceId").Value;
            if (!_instanceManager.PostShutdown(instanceId))
            {
                _logger.Write(LogType.Error, $"Shutdown instance failed on {instanceId} from {client.IP}");
                client.SendPacket("err", new { message = "Could not send shutdown command." });
            }
        }

        [WebSocketPacket("trm", AccountRole.Operator)]
        private void OnTerminateInstance(WebSocketClient client, JsonObject json)
        {
            var instanceId = json.GetMember<JsonString>("instanceId").Value;
            if (!_instanceManager.TerminateInstance(instanceId))
            {
                _logger.Write(LogType.Error, $"Terminate instance failed on {instanceId} from {client.IP}");
                client.SendPacket("err", new { message = "Could not terminate process." });
            }
        }

        private void SendInstanceList(WebSocketClient client)
        {
            var tag = client.Tag as WebSocketTag;
            
            var list = new List<object>();

            foreach (var instance in _instanceManager.Instances)
            {
                var lastLog = InstanceLogManager.GetLast(instance.Id, 200);
                var instanceLog = new List<string>(lastLog.Count());

                foreach (var item in lastLog)
                {
                    instanceLog.Add($"[{tag.User.GetLocalDateTime(item.Time).ToString("HH:mm:ss")}] {item.Text}");
                }

                list.Add(new
                {
                    instanceId = instance.Id,
                    packageName = instance.Package.Name,
                    status = instance.Status,
                    address = _configuration.MinecraftHostname + ":" + instance.BindingInterface.Port,
                    lastLog = instanceLog
                });
            }

            client.SendPacket("lst", new
            {
                instances = list
            });
        }
    }
}
