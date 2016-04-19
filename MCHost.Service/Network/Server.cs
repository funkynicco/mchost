﻿using MCHost.Framework;
using MCHost.Framework.Minecraft;
using MCHost.Framework.Network;
using MCHost.Service.Minecraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Service.Network
{
    public class ServerClient : BaseClient
    {
        public StringBuilder Buffer { get; private set; }
        public DateTime LastDataReceived { get; set; }

        public ServerClient(Socket socket) :
            base(socket)
        {
            Buffer = new StringBuilder(256);
            LastDataReceived = DateTime.UtcNow;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {

            }

            base.Dispose(disposing);
        }

        public void Send(string text)
        {
            var data = Encoding.GetEncoding(1252).GetBytes(text);
            var pos = 0;
            try
            {
                while (pos < data.Length)
                {
                    pos += Socket.Send(data, pos, data.Length - pos, SocketFlags.None);
                }
            }
            catch
            {
                Disconnect();
                throw;
            }
        }
    }

    public interface IServer
    {
        void BroadcastInstanceStatus(string instanceId, InstanceStatus status);
        void BroadcastInstanceLog(string instanceId, string text);
    }

    public partial class Server : NetworkServer<ServerClient>, IServer
    {
        enum PacketMethodParameters
        {
            None = 0,
            Client = 1,
            Content = 2,
            ClientAndContent = 3
        }

        class PacketMethod
        {
            public string Header { get; private set; }
            public MethodInfo Method { get; private set; }
            public PacketMethodParameters Parameters { get; private set; }

            public PacketMethod(string header, MethodInfo method, PacketMethodParameters parameters)
            {
                Header = header;
                Method = method;
                Parameters = parameters;
            }
        }

        protected readonly Encoding _encoding = Encoding.GetEncoding(1252);
        private readonly Dictionary<string, PacketMethod> _packetMethods = new Dictionary<string, PacketMethod>();

        private readonly ILogger _logger;
        private readonly IDatabase _database;
        private IInstanceManager _instanceManager;

        private DateTime _nextCheckLastData = DateTime.UtcNow.AddSeconds(1);

        public Server(ILogger logger, IDatabase database)
        {
            _logger = logger;
            _database = database;

            foreach (var method in GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var attribute = method.GetCustomAttribute<PacketAttribute>();
                if (attribute != null)
                {
                    var name = method.Name;
                    if (name.ToLower().StartsWith("on"))
                        name = name.Substring(2);

                    if (attribute.IsHeaderSet)
                        name = attribute.Header.ToLower();

                    if (_packetMethods.ContainsKey(name.ToLower()))
                        throw new InvalidProgramException($"Duplicate names of packet method '{method.Name}'. Overloading or casing is not supported.");

                    PacketMethodParameters parameterType;

                    var parameters = method.GetParameters();

                    switch (parameters.Length)
                    {
                        case 0:
                            parameterType = PacketMethodParameters.None;
                            break;
                        case 1:
                            if (string.Compare(parameters[0].Name, "client", true) == 0)
                            {
                                parameterType = PacketMethodParameters.Client;
                                if (parameters[0].ParameterType != typeof(ServerClient))
                                    throw new InvalidProgramException($"Invalid dependency injection parameter type for method: {method.Name}");
                            }
                            else if (string.Compare(parameters[0].Name, "content", true) == 0)
                            {
                                parameterType = PacketMethodParameters.Content;
                                if (parameters[0].ParameterType != typeof(string))
                                    throw new InvalidProgramException($"Invalid dependency injection parameter type for method: {method.Name}");
                            }
                            else
                                throw new InvalidProgramException($"Unknown dependency injection parameter {parameters[0].Name} in method {method.Name}");
                            break;
                        case 2:
                            if (string.Compare(parameters[0].Name, "client", true) != 0 ||
                                string.Compare(parameters[1].Name, "content", true) != 0)
                                throw new InvalidProgramException($"The expected parameters for method '{method.Name}' are not provided or the order is wrong, it should be client and then content.");
                            parameterType = PacketMethodParameters.ClientAndContent;

                            if (parameters[0].ParameterType != typeof(ServerClient))
                                throw new InvalidProgramException($"Invalid dependency injection parameter type for method: {method.Name}");
                            if (parameters[1].ParameterType != typeof(string))
                                throw new InvalidProgramException($"Invalid dependency injection parameter type for method: {method.Name}");

                            break;
                        default:
                            throw new InvalidProgramException($"Too many parameters expected in dependency injection for method: {method.Name}");
                    }

                    _packetMethods.Add(name.ToLower(), new PacketMethod(name, method, parameterType));
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            }

            base.Dispose(disposing);
        }

        public void SetInstanceManager(IInstanceManager instanceManager)
        {
            _instanceManager = instanceManager;
        }

        protected override ServerClient AllocateClient(Socket socket)
        {
            return new ServerClient(socket);
        }

        protected override void FreeClient(ServerClient client)
        {
            client.Dispose();
        }

        protected override void OnClientConnected(ServerClient client)
        {
            _logger.Write(LogType.Notice, $"{client.Socket.RemoteEndPoint} connected");
        }

        protected override void OnClientDisconnected(ServerClient client)
        {
            _logger.Write(LogType.Notice, $"{client.Socket.RemoteEndPoint} disconnected");
        }

        protected override void OnClientData(ServerClient client, byte[] buffer, int offset, int length)
        {
            //Console.WriteLine($"[{client.Socket.RemoteEndPoint}] " + _encoding.GetString(buffer, 0, length));
            client.LastDataReceived = DateTime.UtcNow;

            for (var i = 0; i < length; ++i)
            {
                if (buffer[offset + i] == '|')
                {
                    // combine previous data into below packet
                    var packet = client.Buffer.ToString() + _encoding.GetString(buffer, offset, i);
                    client.Buffer.Clear();

                    if (packet.Length > 0)
                        ParsePacket(client, packet);

                    if (client.IsDisconnect)
                        return;

                    if (i + 1 < length)
                        OnClientData(client, buffer, i + 1, length - (i + 1)); // recursive on the rest of the data (if any)

                    return;
                }
            }

            client.Buffer.Append(_encoding.GetString(buffer, offset, length));
        }

        private void ParsePacket(ServerClient client, string packet)
        {
            Console.WriteLine($"[{client.Socket.RemoteEndPoint}] Packet: '{packet}'");

            int pos;
            string header = null;
            string content = string.Empty;

            if ((pos = packet.IndexOf(' ')) != -1)
            {
                header = packet.Substring(0, pos);
                content = packet.Substring(pos + 1);
            }
            else
                header = packet;

            header = header.ToLower();

            PacketMethod packetMethod;
            if (!_packetMethods.TryGetValue(header, out packetMethod))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Invalid header in packet from {client.Socket.RemoteEndPoint}: '{packet}'");
                Console.ForegroundColor = ConsoleColor.Gray;
                client.Disconnect();
                return;
            }

            object[] parameters;

            switch (packetMethod.Parameters)
            {
                case PacketMethodParameters.Client:
                    parameters = new object[] { client };
                    break;
                case PacketMethodParameters.Content:
                    parameters = new object[] { content };
                    break;
                case PacketMethodParameters.ClientAndContent:
                    parameters = new object[] { client, content };
                    break;
                default:
                    parameters = new object[] { };
                    break;
            }

            packetMethod.Method.Invoke(this, parameters);
        }

        public override void Process()
        {
            base.Process();

            var now = DateTime.UtcNow;
            if (now >= _nextCheckLastData)
            {
                foreach (var client in Clients)
                {
                    if (!client.IsDisconnect &&
                        (now - client.LastDataReceived).TotalMinutes >= 5)
                    {
                        _logger.Write(LogType.Warning, $"Client {client.Socket.RemoteEndPoint} kicked for inactivity (5 min)");
                        client.Disconnect();
                    }
                }

                _nextCheckLastData = now.AddSeconds(1);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class PacketAttribute : Attribute
    {
        public bool IsHeaderSet { get; private set; }
        public string Header { get; private set; }

        public PacketAttribute()
        {
            IsHeaderSet = false;
        }

        public PacketAttribute(string header)
        {
            IsHeaderSet = true;
            Header = header;
        }
    }

    public class PacketDataException : Exception
    {
        public PacketDataException(string message) :
            base(message)
        {
        }
    }
}