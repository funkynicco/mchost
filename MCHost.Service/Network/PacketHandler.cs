using MCHost.Framework;
using MCHost.Framework.Minecraft;
using MCHost.Framework.Network;
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
        /*[Packet(Header.List)]
        void OnListInstances(ServerClient client, int requestId)
        {
            var packet = new Packet(Header.List, requestId);

            packet.Write(_instanceManager.Instances.Count());
            foreach (var instance in _instanceManager.Instances)
            {
                packet.Write(instance.Id);
                packet.Write((int)instance.Status);
                packet.Write(instance.Package.Name);
                instance.Configuration.Serialize(packet);
            }

            client.Send(packet);
        }

        [Packet(Header.Command)]
        void OnCommand(ServerClient client, int requestId, DataBuffer buffer)
        {
            var instanceId = buffer.ReadString();
            var command = buffer.ReadString();

            if (!Regex.IsMatch(instanceId, "^[a-f0-9]{16}$"))
            {
                _logger.Write(LogType.Warning, $"(Ignored) Client {client.Socket.RemoteEndPoint} sent invalid instance id in CMD packet: {instanceId}");
                return;
            }

            if (command.Length == 0)
            {
                _logger.Write(LogType.Warning, $"(Ignored) Client {client.Socket.RemoteEndPoint} sent empty command in CMD packet.");
                return;
            }

            if (!_instanceManager.PostCommand(instanceId, command))
            {
                _logger.Write(LogType.Warning, "(Failed) Instance not found or running in CMD packet: " + instanceId);
                client.SendError("Instance not found.", requestId);
            }
            else
                _logger.Write(LogType.Notice, $"[{instanceId}]> {command}");
        }

        [Packet(Header.New)]
        void OnNewInstance(ServerClient client, int requestId, DataBuffer buffer)
        {
            var packageName = buffer.ReadString();
            var configuration = InstanceConfiguration.Deserialize(buffer);

            var package = _database.GetPackages().Where((a) => a.Name == packageName).FirstOrDefault();
            if (package == null)
            {
                client.SendError("The package was not found.", requestId);
                return;
            }

            try
            {
                configuration.Validate();
            }
            catch (Exception ex)
            {
                _logger.Write(LogType.Warning, $"(Ignored) Invalid configuration from {client.Socket.RemoteEndPoint}: {ex.Message}");
                client.SendError("Invalid configuration sent.", requestId);
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
                client.SendError(ex.Message, requestId);
                return;
            }

            instance.Configuration = configuration;

            var packet = new Packet(Header.New, requestId);
            packet.Write(instance.Id);
            packet.Write(package.Name);
            client.Send(packet);

            instance.Start();
        }

        [Packet(Header.Terminate)]
        void OnTerminateInstance(ServerClient client, int requestId, DataBuffer buffer)
        {
            var instanceId = buffer.ReadString();

            if (!Regex.IsMatch(instanceId, "^[a-f0-9]{16}$"))
            {
                _logger.Write(LogType.Warning, $"(Ignored) Client {client.Socket.RemoteEndPoint} sent invalid instance id in TRM packet: {instanceId}");
                return;
            }

            if (_instanceManager.TerminateInstance(instanceId))
            {
                _logger.Write(LogType.Warning, "(Failed) Instance not found or running in TRM packet: " + instanceId);
                client.SendError("Instance not found.", requestId);
            }
            else
                _logger.Write(LogType.Warning, $"Terminating instance {instanceId}");
        }

        [Packet(Header.InstanceConfiguration)]
        void OnGetInstanceConfiguration(ServerClient client, int requestId, DataBuffer buffer)
        {
            var instanceId = buffer.ReadString();

            var configuration = _instanceManager.GetInstanceConfiguration(instanceId);
            if (configuration == null)
            {
                client.SendError("Instance not found.", requestId);
                return;
            }

            var packet = new Packet(Header.InstanceConfiguration, requestId);
            packet.Write(instanceId);
            configuration.Serialize(packet);
            client.Send(packet);
        }*/
    }
}
