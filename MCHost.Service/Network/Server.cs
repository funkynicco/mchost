using MCHost.Framework;
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
        public DataBuffer Buffer { get; private set; }
        public DateTime LastDataReceived { get; set; }

        public ServerClient(Socket socket) :
            base(socket)
        {
            Buffer = new DataBuffer();
            LastDataReceived = DateTime.UtcNow;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {

            }

            base.Dispose(disposing);
        }

        private void Send(byte[] buffer, int offset, int length)
        {
            try
            {
                while (offset < length)
                {
                    offset += Socket.Send(buffer, offset, length - offset, SocketFlags.None);
                }
            }
            catch
            {
                Disconnect();
                throw;
            }
        }

        public void Send(Packet packet)
        {
            int length;
            var buffer = packet.GetInternalBuffer(out length);

            Send(buffer, 0, length);
        }

        public void Send(DataBuffer buffer)
        {
            Send(buffer.InternalBuffer, 0, buffer.Length);
        }

        public void SendError(string text, int requestId)
        {
            var packet = new Packet(Header.Error, requestId);
            packet.Write(text);
            Send(packet);
        }
    }

    public interface IServer
    {
        void BroadcastInstanceStatus(string instanceId, InstanceStatus status);
        void BroadcastInstanceLog(string instanceId, string text);
    }

    public partial class Server : NetworkServer<ServerClient>, IServer
    {
        enum PacketMethodParameter
        {
            Client,
            RequestId,
            Buffer
        }

        class PacketMethod
        {
            public Header Header { get; private set; }
            public MethodInfo Method { get; private set; }

            private readonly List<PacketMethodParameter> _parameters = new List<PacketMethodParameter>();
            public IEnumerable<PacketMethodParameter> Parameters { get { return _parameters; } }

            private readonly object _invocationInstance;
            private object[] _invocationParameters;

            public PacketMethod(object invocationInstance, Header header, MethodInfo method)
            {
                _invocationInstance = invocationInstance;
                Header = header;
                Method = method;
            }

            public void AddParameter(PacketMethodParameter parameter)
            {
                _parameters.Add(parameter);
            }

            public void UpdateParameters()
            {
                _invocationParameters = new object[_parameters.Count];
            }

            public object Invoke(ServerClient client, int requestId, DataBuffer buffer)
            {
                int i = 0;
                foreach (var param in _parameters)
                {
                    switch (param)
                    {
                        case PacketMethodParameter.Client: _invocationParameters[i++] = client; break;
                        case PacketMethodParameter.RequestId: _invocationParameters[i++] = requestId; break;
                        case PacketMethodParameter.Buffer: _invocationParameters[i++] = buffer; break;
                    }
                }

                return Method.Invoke(_invocationInstance, _invocationParameters);
            }
        }

        private readonly Dictionary<Header, PacketMethod> _packetMethods = new Dictionary<Header, PacketMethod>();

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
                    if (_packetMethods.ContainsKey(attribute.Header))
                        throw new InvalidProgramException($"Duplicate headers of packet method '{method.Name}'. Overloading is not supported.");

                    var packetMethod = new PacketMethod(this, attribute.Header, method);

                    var parameters = method.GetParameters();
                    foreach (var parameter in parameters)
                    {
                        if (string.Compare(parameter.Name, "client", true) == 0)
                        {
                            if (parameter.ParameterType != typeof(ServerClient))
                                throw new InvalidProgramException($"Invalid dependency injection parameter type for method: {method.Name}");

                            packetMethod.AddParameter(PacketMethodParameter.Client);
                        }
                        else if (string.Compare(parameter.Name, "requestId", true) == 0)
                        {
                            if (parameter.ParameterType != typeof(int))
                                throw new InvalidProgramException($"Invalid dependency injection parameter type for method: {method.Name}");

                            packetMethod.AddParameter(PacketMethodParameter.RequestId);
                        }
                        else if (string.Compare(parameter.Name, "buffer", true) == 0)
                        {
                            if (parameter.ParameterType != typeof(DataBuffer))
                                throw new InvalidProgramException($"Invalid dependency injection parameter type for method: {method.Name}");

                            packetMethod.AddParameter(PacketMethodParameter.Buffer);
                        }
                        else
                            throw new InvalidProgramException($"Invalid dependency parameter for method '{method.Name}': ({parameter.ParameterType.FullName}) {parameter.Name}");
                    }

                    packetMethod.UpdateParameters();
                    _packetMethods.Add(attribute.Header, packetMethod);
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
            client.LastDataReceived = DateTime.UtcNow;

            try
            {
                var buf = client.Buffer;

                buf.Offset = buf.Length;
                buf.Write(buffer, offset, length);

                while (buf.Length >= Packet.HeaderSize)
                {
                    buf.Offset = 0;
                    var header = buf.ReadInt32();
                    var requestId = buf.ReadInt32();
                    var packetSize = buf.ReadInt32();

                    if (buf.Length - buf.Offset < packetSize)
                        return; // need more data

                    if (header <= 0 ||
                        header >= (int)Header.MaxHeader)
                    {
                        _logger.Write(LogType.Warning, $"Invalid header 0x{header.ToString("x8")} from {client.Socket.RemoteEndPoint}");
                        client.Disconnect();
                        return;
                    }

                    ProcessPacket(client, (Header)header, requestId, buf);

                    buf.Remove(Packet.HeaderSize + packetSize);
                }
            }
            catch (EndOfBufferException)
            {
                _logger.Write(LogType.Warning, $"(EndOfBufferException) on {client.Socket.RemoteEndPoint}");
                client.Disconnect();
            }
        }

        private void ProcessPacket(ServerClient client, Header header, int requestId, DataBuffer buffer)
        {
            //Console.WriteLine($"[{client.Socket.RemoteEndPoint}] Header: {header}");

            if (header == Header.Ping) // don't need to process any data for this packet
                return;

            PacketMethod packetMethod;
            if (!_packetMethods.TryGetValue(header, out packetMethod))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Unhandled header in packet from {client.Socket.RemoteEndPoint}: '{header}'");
                Console.ForegroundColor = ConsoleColor.Gray;
                client.Disconnect();
                return;
            }

            try
            {
                packetMethod.Invoke(client, requestId, buffer);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException;
            }
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
        public Header Header { get; private set; }

        public PacketAttribute(Header header)
        {
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