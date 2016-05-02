using MCHost.Framework;
using MCHost.Framework.Json;
using MCHost.Framework.Minecraft;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace MCHost.Web
{
    public interface IWebSocketClient
    {
        StringBuilder Buffer { get; }

        Task Send(string text);
        Task Send(string header, object data);
    }

    public interface IWebSocketClientHandler
    {
        void AcceptWebSocketRequest(HttpContext httpContext);
        Task Broadcast(string text);
        Task Broadcast(string header, object data);
    }

    public class WebSocketClientHandler : IWebSocketClientHandler
    {
        class WebSocketClient : IWebSocketClient
        {
            private readonly ILogger _logger;
            private readonly WebSocketClientHandler _clientHandler;

            private WebSocketContext _context;

            private readonly int _id;
            public int Id { get { return _id; } }

            public StringBuilder Buffer { get; private set; } = new StringBuilder();

            public WebSocketClient(ILogger logger, WebSocketClientHandler clientHandler, int id)
            {
                _logger = logger;
                _clientHandler = clientHandler;
                _id = id;
            }

            public async Task HandleWebSocket(WebSocketContext context)
            {
                _context = context;

                var buffer = new byte[65536];
                var sb = new StringBuilder(1024);
                var encoding = Encoding.UTF8;

                await _clientHandler.OnWebSocketOpen(this);

                while (context.WebSocket.State == WebSocketState.Open)
                {
                    var result = await context.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await context.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        lock (_clientHandler._lock)
                        {
                            _clientHandler._clients.Remove(Id);
                        }
                        await _clientHandler.OnWebSocketClose(this);
                        return;
                    }

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        await context.WebSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Cannot accept binary frame", CancellationToken.None);
                        lock (_clientHandler._lock)
                        {
                            _clientHandler._clients.Remove(Id);
                        }
                        await _clientHandler.OnWebSocketClose(this);
                        return;
                    }

                    // receive all data
                    if (!result.EndOfMessage)
                    {
                        do
                        {
                            sb.Append(encoding.GetString(buffer, 0, result.Count));
                            result = await context.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        }
                        while (!result.EndOfMessage);
                    }
                    else
                        sb.Append(encoding.GetString(buffer, 0, result.Count));

                    // data received
                    await _clientHandler.OnWebSocketData(this, sb.ToString());
                    sb.Clear();
                }
            }

            public async Task Send(ArraySegment<byte> data)
            {
                await _context.WebSocket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
            }

            public async Task Send(string text)
            {
                _logger.Write(LogType.Notice, "Client.Send " + text);
                await Send(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)));
            }

            public async Task Send(string header, object data)
            {
                var json = JsonConvert.SerializeObject(data);
                await Send($"{header.ToLower()} {Utilities.EscapeWebSocketContent(json)}|");
            }
        }

        private readonly Dictionary<int, WebSocketClient> _clients = new Dictionary<int, WebSocketClient>();
        private readonly Queue<int> _idQueue = new Queue<int>();
        private int _nextId = 1;
        private readonly object _lock = new object();
        private readonly IHostClient _hostClient;
        private readonly ILogger _logger;

        public WebSocketClientHandler(IHostClient hostClient, ILogger logger)
        {
            _hostClient = hostClient;
            _logger = logger;
        }

        private int AllocateId()
        {
            lock (_lock)
            {
                if (_idQueue.Count == 0)
                {
                    for (int i = 0; i < 16; ++i)
                        _idQueue.Enqueue(_nextId++);
                }

                return _idQueue.Dequeue();
            }
        }

        public virtual void AcceptWebSocketRequest(HttpContext httpContext)
        {
            WebSocketClient client;

            lock (_lock)
            {
                client = new WebSocketClient(_logger, this, AllocateId());
                _clients.Add(client.Id, client);
            }

            httpContext.AcceptWebSocketRequest(client.HandleWebSocket);
        }

        public virtual async Task Broadcast(string text)
        {
            var tasks = new List<Task>();

            lock (_lock)
            {
                foreach (var client in _clients.Values)
                {
                    var task = client.Send(text);
                    _logger.Write(LogType.Notice, "Broadcast " + text);
                    tasks.Add(task);
                    task.Start();
                }
            }

            await Task.WhenAll(tasks);
        }

        public virtual async Task Broadcast(string header, object data)
        {
            var json = JsonConvert.SerializeObject(data);
            await Broadcast($"{header.ToLower()} {Utilities.EscapeWebSocketContent(json)}|");
        }

        // handlers
        protected async virtual Task OnWebSocketOpen(IWebSocketClient client)
        {
            _logger.Write(LogType.Normal, "OnWebSocketOpen (cli: " + _clients.Count + ")\r\n");

            //await client.Send("new {\"instanceId\":\"531853186\",\"packageName\":\"hello world\"}|");

            await client.Send("test", client);
        }

        protected async virtual Task OnWebSocketClose(IWebSocketClient client)
        {
            _logger.Write(LogType.Normal, "OnWebSocketClose (cli: " + _clients.Count + ")\r\n");
        }

        protected async virtual Task OnWebSocketData(IWebSocketClient client, string data)
        {
            _logger.Write(LogType.Normal, "OnWebSocketData: " + data + "\r\n");

            var buffer = client.Buffer.ToString() + data;
            int pos;

            while ((pos = buffer.IndexOf('|', client.Buffer.Length)) != -1)
            {
                var header = buffer.Substring(0, pos);
                buffer = buffer.Substring(pos + 1);

                if ((pos = header.IndexOf(' ')) != -1)
                {
                    await ProcessPacket(
                        client,
                        header.Substring(0, pos).ToLower(),
                        header.Substring(pos + 1));
                }
                else
                    await ProcessPacket(client, header.ToLower(), string.Empty);
            }
        }

        protected async virtual Task ProcessPacket(IWebSocketClient client, string header, string content)
        {
            _logger.Write(LogType.Normal, "Pack(" + header + "): " + content + "\r\n");

            try
            {
                // implement json parse error exception so that we dont crash the entire app but only disconnect this client
                var data = JsonParser.Parse<JsonObject>(content);
                if (header == "new")
                {
                    var packageName = data.GetMember<JsonString>("packageName").Value;
                    var instanceConfiguration = InstanceConfiguration.Default;

                    var instanceId = _hostClient.CreateInstance(
                        packageName,
                        instanceConfiguration);

                    await Broadcast("new", new
                    {
                        instanceId = instanceId,
                        packageName = packageName
                    });
                }
            }
            catch (Framework.Json.JsonException ex)
            {
                _logger.Write(
                    LogType.Error,
                    $"[{header}] Json protocol error: ({ex.GetType().Name}) '{ex.Message}' in packet: {content}");
            }
        }
    }
}