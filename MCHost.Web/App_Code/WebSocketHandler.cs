using MCHost.Framework;
using MCHost.Framework.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace MCHost.Web
{
    public class WebSocketHandler : IHttpHandler
    {
        private readonly IDatabase _database;
        private readonly ILogger _logger;
        private readonly IWebSocketClientHandler _webSocketClientHandler;

        public bool IsReusable { get { return true; } }

        public WebSocketHandler(IDatabase database, ILogger logger, IWebSocketClientHandler webSocketClientHandler)
        {
            _database = database;
            _logger = logger;
            _webSocketClientHandler = webSocketClientHandler;
        }

        public void ProcessRequest(HttpContext context)
        {
            if (!context.IsWebSocketRequest)
            {
                context.Response.StatusCode = 404;
                return;
            }

            if (RequestAccessAuthorizer.Authorize(_database) == null)
            {
                context.Response.StatusCode = 404;
                return;
            }

            _webSocketClientHandler.AcceptWebSocketRequest(context);
        }
    }
}