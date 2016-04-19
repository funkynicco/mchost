using MCHost.Framework;
using MCHost.Framework.Minecraft;
using MCHost.Service.Minecraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MCHost.Service.Network
{
    public partial class Server
    {
        [Packet("LST")]
        void OnListInstances(ServerClient client)
        {
            var sb = new StringBuilder(128);
            sb.Append("LST ");
            int n = 0;

            foreach (var instance in _instanceManager.Instances)
            {
                if (n++ > 0)
                    sb.Append('$');

                sb.Append($"{instance.Id}:{(int)instance.Status}:{instance.Package.Name.Replace(":", "&#58;")}");
            }

            sb.Append('|');

            client.Send(sb.ToString());
        }

        [Packet("CMD")]
        void OnCommand(ServerClient client, string content)
        {
            var pos = content.IndexOf(':');
            if (pos == -1)
            {
                _logger.Write(LogType.Warning, $"(Ignored) Client {client.Socket.RemoteEndPoint} sent invalid CMD packet: {content}");
                return;
            }

            var instanceId = content.Substring(0, pos);
            var command = content.Substring(pos + 1).Trim();

            if (!Regex.IsMatch(instanceId, "^[a-f0-9]{16}$"))
            {
                _logger.Write(LogType.Warning, $"(Ignored) Client {client.Socket.RemoteEndPoint} sent invalid instance id in CMD packet: {content}");
                return;
            }

            if (command.Length == 0)
            {
                _logger.Write(LogType.Warning, $"(Ignored) Client {client.Socket.RemoteEndPoint} sent empty command in CMD packet: {content}");
                return;
            }

            if (!_instanceManager.PostCommand(instanceId, command))
            {
                _logger.Write(LogType.Warning, "(Failed) Instance not found or running in CMD packet: " + instanceId);
                client.Send("ERR Instance not found.|");
            }
            else
                _logger.Write(LogType.Notice, $"[{instanceId}]> {command}");
        }

        [Packet("NEW")]
        void OnNewInstance(ServerClient client, string content)
        {
            var pos = content.IndexOf(':');
            if (pos == -1)
            {
                _logger.Write(LogType.Warning, $"(Ignored) Client {client.Socket.RemoteEndPoint} sent invalid NEW packet: {content}");
                return;
            }

            var packageName = content.Substring(0, pos);
            content = content.Substring(pos + 1).Trim();

            var package = _database.GetPackages().Where((a) => a.Name == packageName).FirstOrDefault();
            if (package == null)
            {
                client.Send("ERR The package was not found.|");
                return;
            }

            InstanceConfiguration instanceConfiguration;

            try
            {
                instanceConfiguration = InstanceConfiguration.Deserialize(content);
                instanceConfiguration.Validate();
            }
            catch (Exception ex)
            {
                _logger.Write(LogType.Warning, $"(Ignored) Invalid configuration from {client.Socket.RemoteEndPoint}: {ex.Message}");
                _logger.Write(LogType.Warning, $"[{client.Socket.RemoteEndPoint}] {content}");
                client.Send("ERR Invalid configuration sent.|");
                return;
            }

            IInstance instance;

            try
            {
                instance = _instanceManager.CreateInstance(package);
            }
            catch (ConcurrentInstancesExceededException ex)
            {
                _logger.Write(LogType.Warning, $"({ex.GetType().Name}) {ex.Message}");
                client.Send($"ERR {ex.Message}|");
                return;
            }

            instance.Configuration = instanceConfiguration;
            client.Send($"NEW {instance.Id}:{package.Name.Replace(":", "&#58;").Replace("|", "&#124;")}|");
            instance.Start();
        }

        [Packet("TRM")]
        void OnTerminateInstance(ServerClient client, string content)
        {
            var instanceId = content;

            if (!Regex.IsMatch(instanceId, "^[a-f0-9]{16}$"))
            {
                _logger.Write(LogType.Warning, $"(Ignored) Client {client.Socket.RemoteEndPoint} sent invalid instance id in TRM packet: {content}");
                return;
            }

            if (_instanceManager.TerminateInstance(instanceId))
            {
                _logger.Write(LogType.Warning, "(Failed) Instance not found or running in TRM packet: " + instanceId);
                client.Send("ERR Instance not found.|");
            }
            else
                _logger.Write(LogType.Warning, $"Terminating instance {instanceId}");
        }

        [Packet("CFG")]
        void OnGetInstanceConfiguration(ServerClient client, string content)
        {
            var instanceId = content;

            var configuration = _instanceManager.GetInstanceConfiguration(instanceId);
            if (configuration == null)
            {
                client.Send("ERR Instance not found.|");
                return;
            }

            var instanceConfigurationData = configuration.Serialize();
            client.Send($"CFG {instanceId}:{instanceConfigurationData}|");
        }
    }
}
