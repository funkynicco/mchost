using MCHost.Framework;
using MCHost.Framework.Json;
using MCHost.Framework.Security;
using MCHost.Service.Minecraft;
using MCHost.WebSockets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Service.Network
{
    public partial class WebSocketService : WebSocketServer
    {
        [AttributeUsage(AttributeTargets.Method)]
        class WebSocketPacketAttribute : Attribute
        {
            public string Header { get; private set; }
            public AccountRole MinimumRole { get; private set; }

            public WebSocketPacketAttribute(string header, AccountRole minimumRole)
            {
                Header = header.ToLower();
                MinimumRole = minimumRole;
            }

            public WebSocketPacketAttribute(string header) :
                this(header, AccountRole.Registered)
            {
            }
        }

        class WebSocketPacketMethod
        {
            public WebSocketService Service { get; private set; }
            public WebSocketPacketAttribute Attribute { get; private set; }
            public MethodInfo Method { get; private set; }

            public WebSocketPacketMethod(WebSocketService service, WebSocketPacketAttribute attribute, MethodInfo method)
            {
                Service = service;
                Attribute = attribute;
                Method = method;
            }

            public void Invoke(WebSocketClient client, JsonObject json)
            {
                var parameters = new object[] { client, json };
                Method.Invoke(Service, parameters);
            }
        }

        class WebSocketTag
        {
            public User User { get; private set; }
            public StringBuilder WebSocketBuffer { get; private set; }

            public WebSocketTag(User user)
            {
                User = user;
                WebSocketBuffer = new StringBuilder(128);
            }
        }

        private readonly ILogger _logger;
        private readonly IDatabase _database;
        private IInstanceManager _instanceManager;
        private readonly Dictionary<string, WebSocketPacketMethod> _packetMethods = new Dictionary<string, WebSocketPacketMethod>();

        public WebSocketService(ILogger logger, IDatabase database, bool isBackendServer) :
            base(isBackendServer)
        {
            _logger = logger;
            _database = database;

            foreach (var method in GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var attribute = method.GetCustomAttribute<WebSocketPacketAttribute>();
                if (attribute != null)
                {
                    if (_packetMethods.ContainsKey(attribute.Header))
                        throw new InvalidProgramException($"Duplicate headers of packet method '{method.Name}'. Overloading is not supported.");

                    _packetMethods.Add(attribute.Header, new WebSocketPacketMethod(this, attribute, method));
                }
            }
        }

        public void SetInstanceManager(IInstanceManager instanceManager)
        {
            _instanceManager = instanceManager;
        }

        protected override bool AuthorizeRequest(WebSocketClient client, HttpHeader header)
        {
            var cookie = header.GetCookie(UserCookie.CookieName);
            if (cookie == null)
            {
                _logger.Write(LogType.Warning, "Cookie not found in request");
                return false;
            }

            _logger.Write(LogType.Notice, "Authorizing WebSocket session ...");

            var user = _database.ResumeSession(cookie);
            if (user == null)
            {
                _logger.Write(LogType.Warning, "ResumeSession failed, key: " + cookie);
                return false;
            }

            client.Tag = new WebSocketTag(user);
            _logger.Write(LogType.Warning, $"[WebSocketService] Authorized {user.DisplayName} from {client.IP}");
            return true;
        }
        protected override void OnClientConnected(WebSocketClient client)
        {
            _logger.Write(LogType.Notice, "OnClientConnected: " + client.IP);
            base.OnClientConnected(client);
        }

        protected override void OnWebSocketOpen(WebSocketClient client)
        {
            _logger.Write(LogType.Notice, "OnWebSocketOpen: " + client.IP);
        }

        protected override void OnWebSocketClosed(WebSocketClient client)
        {
            _logger.Write(LogType.Notice, "OnWebSocketClosed: " + client.IP);
        }

        protected override void OnWebSocketData(WebSocketClient client, string data)
        {
            var tag = client.Tag as WebSocketTag;

            _logger.Write(LogType.Notice, "OnWebSocketData: " + client.IP + " => " + data);

            var buffer = tag.WebSocketBuffer.ToString() + data;
            int pos;

            while ((pos = buffer.IndexOf('|', tag.WebSocketBuffer.Length)) != -1)
            {
                var header = buffer.Substring(0, pos);
                buffer = buffer.Substring(pos + 1);

                if ((pos = header.IndexOf(' ')) != -1)
                {
                    ProcessPacket(
                        client,
                        tag,
                        header.Substring(0, pos).ToLower(),
                        header.Substring(pos + 1));
                }
                else
                    ProcessPacket(client, tag, header.ToLower(), string.Empty);
            }

            tag.WebSocketBuffer.Clear();
            if (buffer.Length > 0)
                tag.WebSocketBuffer.Append(buffer);
        }

        private void ProcessPacket(WebSocketClient client, WebSocketTag tag, string header, string data)
        {
            WebSocketPacketMethod method;
            if (!_packetMethods.TryGetValue(header, out method))
            {
                _logger.Write(LogType.Warning, $"Invalid header sent from {client.IP} => {header}");
                return;
            }

            if (tag.User.Role < method.Attribute.MinimumRole)
            {
                _logger.Write(LogType.Warning, $"{client.IP} Insufficient privileges to execute header => {method.Method.Name}");
                return;
            }

            JsonObject obj;
            try
            {
                obj = JsonParser.Parse<JsonObject>(data);
            }
            catch (JsonParseException ex)
            {
                _logger.Write(LogType.Warning, $"Invalid json data from {client.IP} => ({ex.GetType().Name}) {ex.Message}");
                return;
            }

            try
            {
                method.Invoke(client, obj);
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException is JsonMemberNotFoundException)
                {
                    _logger.Write(LogType.Warning, $"{client.IP} - (JsonMemberNotFoundException) " + ex.InnerException.Message);
                    return;
                }
                else if (ex.InnerException is JsonTypeUnexpectedException)
                {
                    _logger.Write(LogType.Warning, $"{client.IP} - (JsonTypeUnexpectedException) " + ex.InnerException.Message);
                    return;
                }

                _logger.Write(LogType.Warning, $"({ex.InnerException.GetType().Name}) {ex.InnerException.Message}\n{ex.InnerException.StackTrace}");
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }
    }

    public static class WebSocketPacketExtensions
    {
        public static void SendPacket(this WebSocketClient client, string header, object data)
        {
            client.SendWebSocketText(header + " " + Utilities.EscapeWebSocketContent(JsonConvert.SerializeObject(data)) + "|");
        }
    }
}
