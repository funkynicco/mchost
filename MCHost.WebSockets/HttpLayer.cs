using MCHost.Framework.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.WebSockets
{
    public partial class WebSocketServer
    {
        private void ProcessHttpData(WebSocketClient client, byte[] buffer, int offset, int length)
        {
            var encoding = Encoding.GetEncoding(1252);

            var data = encoding.GetString(buffer, offset, length);

            int pos;
            bool hasRowFeed;
            while ((pos = HttpHeader.FindHttpHeaderEnd(ref data, out hasRowFeed)) != -1)
            {
                if (client.Buffer.Length > 0)
                {
                    pos += client.Buffer.Length;
                    data = encoding.GetString(client.Buffer.InternalBuffer, 0, client.Buffer.Length) + data;
                    client.Buffer.Reset();
                }

                var packet = data.Substring(0, pos);
                data = data.Substring(pos + (hasRowFeed ? 4 : 2));

                if (packet.Length > 0)
                {
                    // log packet
                    //System.IO.File.AppendAllText("web_datalog.txt", string.Format("[{0}]\r\n{1}\r\n\r\n", DateTime.Now.ToString("HH:mm:ss"), packet));

                    HttpHeader header;
                    try
                    {
                        foreach (char c in packet)
                        {
                            if (c == 0)
                                throw new UrlDecodingException(string.Empty);
                        }

                        if (packet.Contains("%0"))
                            throw new UrlDecodingException(string.Empty);

                        header = HttpHeader.ParseHeader(ref packet);
                    }
                    catch (UrlDecodingException ex)
                    {
                        client.SendErrorResponse(500, "Internal Error", "Malformed URL" +
                            (ex.Message.Length > 0 ? (": " + ex.Message) : "."));
                        client.Disconnect(ex.Message);
                        return;
                    }

                    if (string.Compare(header.Version, "HTTP/1.1", true) != 0)
                    {
                        client.Disconnect("Invalid HTTP verson: " + header.Version);
                        return;
                    }

                    if (header.Method == HttpRequestMethod.Unknown)
                    {
                        client.Disconnect("Invalid request method.");
                        return;
                    }

                    ////////////////////////////////////////////////////////////////////////////////////////////

                    // Reverse Proxy implementation
                    /*
                        X-Original-URL: /app/assets/img/icon_plus.png
                        X-Forwarded-For: 78.70.113.225:65346
                        X-ARR-LOG-ID: 7b5980f7-9854-45f4-91cf-6cfba7cb29a9
                    */
                    if (_isBackendServer)
                    {
                        string reverseProxyIp = null;
                        if (header.GetParameter("X-Forwarded-For", ref reverseProxyIp))
                        {
                            int colonPos;
                            if ((colonPos = reverseProxyIp.IndexOf(':')) != -1)
                                reverseProxyIp = reverseProxyIp.Substring(0, colonPos);

                            if (reverseProxyIp.Length > 0)
                            {
                                //Logger.Log(LogType.Warning, "X-Forwarded-For in request was empty.");
                                client.SetUserAddress(reverseProxyIp);
                            }
                        }
                    }

                    ////////////////////////////////////////////////////////////////////////////////////////////

                    long contentLength = 0;
                    header.GetParameter("Content-Length", ref contentLength); // does not edit contentLength if the parameter doesn't exist

                    if (contentLength != 0) // we only handle websocket initial http requests, no content is expected
                    {
                        client.Disconnect("Client sent unexpected content of " + contentLength + " bytes");
                        return;
                    }

                    InternalHandleRequest(client, header);

                    if (client.IsDisconnect)
                        return;
                }
            }

            if (data.Length > 0)
            {
                var binaryData = encoding.GetBytes(data);
                client.Buffer.Offset = client.Buffer.Length;
                client.Buffer.Write(binaryData, 0, binaryData.Length);
            }
        }

        protected virtual bool AuthorizeRequest(WebSocketClient client, HttpHeader header)
        {
            return true;
        }

        private void InternalHandleRequest(WebSocketClient client, HttpHeader header)
        {
            if (header.Method != HttpRequestMethod.Get)
            {
                client.Disconnect("Unexpected request method for websocket handshake: " + header.Method);
                return;
            }

            if (!AuthorizeRequest(client, header))
            {
                client.Disconnect("Client authorization failed.");
                return;
            }

            string upgrade = "";
            string connection = "";
            string webSocketKey = "";
            string webSocketProtocol = null;
            int webSocketVersion = 1;

            header.GetParameter("Sec-WebSocket-Protocol", ref webSocketProtocol);
            header.GetParameter("Sec-WebSocket-Version", ref webSocketVersion);

            if (!header.GetParameter("Upgrade", ref upgrade) ||
                !header.GetParameter("Connection", ref connection) ||
                !header.GetParameter("Sec-WebSocket-Key", ref webSocketKey) ||
                string.Compare(upgrade, "websocket", true) != 0 ||
                string.Compare(connection, "upgrade", true) != 0)
            {
                client.SendErrorResponse(500, "Internal Error", "Invalid websocket request.");
                return;
            }

            var responseKey = GenerateWebSocketAcceptKey(webSocketKey);

            var response = new StringBuilder(256);
            response.AppendFormat(
                "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {0}\r\n",
                responseKey);

            if (webSocketProtocol != null)
                response.AppendFormat("Sec-WebSocket-Protocol: {0}\r\n", webSocketProtocol);

            response.Append("\r\n");

            client.Send(DataBuffer.FromString(response.ToString()));
            client.Buffer.Reset();
            client.UpgradeToWebSocket();

            OnWebSocketOpen(client);
        }
    }
}
