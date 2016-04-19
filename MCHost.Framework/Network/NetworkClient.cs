using MCHost.Framework.Minecraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MCHost.Framework.Network
{
    public class NetworkClient : IDisposable
    {
        private readonly Thread _thread;
        private readonly AutoResetEvent _connectEvent = new AutoResetEvent(false);
        private readonly ManualResetEvent _shutdownEvent = new ManualResetEvent(false);

        private EndPoint _endPoint;
        private readonly object _lock = new object();

        private readonly Encoding _encoding = Encoding.GetEncoding(1252);
        private readonly StringBuilder _buffer = new StringBuilder();

        private readonly StringBuilder _sendBuffer = new StringBuilder();
        private readonly object _sendBufferLock = new object();
        private readonly AutoResetEvent _sendBufferEvent = new AutoResetEvent(false);

        public NetworkClient()
        {
            _thread = new Thread(WorkerThread);
            _thread.Start();
        }

        public void Dispose()
        {
            _shutdownEvent.Set();
            _thread.Join();

            _connectEvent.Dispose();
            _shutdownEvent.Dispose();
            _sendBufferEvent.Dispose();
        }

        public void Connect(string address, int port)
        {
            lock (_lock)
            {
                _endPoint = new IPEndPoint(IPAddress.Parse(address), port);
                _connectEvent.Set();
            }
        }

        public void Send(string text)
        {
            lock (_sendBufferLock)
            {
                _sendBuffer.Append(text);
                _sendBufferEvent.Set();
            }
        }

        private void WorkerThread()
        {
            var events = new WaitHandle[] { _connectEvent, _shutdownEvent };

            while (true)
            {
                int n = WaitHandle.WaitAny(events);
                if (n == 1) // shutdown
                    break;

                try
                {
                    EndPoint endPoint;
                    lock (_lock)
                    {
                        endPoint = _endPoint;
                    }

                    using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        OnConnecting();

                        try
                        {
                            socket.Connect(endPoint);
                        }
                        catch (Exception ex)
                        {
                            OnConnectionFailed(ex);
                            throw new ExitClientLoopException();
                        }

                        OnConnected();

                        var buffer = new byte[65536];
                        bool poll;
                        int len;
                        var events2 = new WaitHandle[] { _sendBufferEvent, _shutdownEvent };

                        var nextSendPing = DateTime.UtcNow.AddMinutes(2);

                        while (true)
                        {
                            var now = DateTime.UtcNow;

                            n = WaitHandle.WaitAny(events2, 50);
                            if (n == 0) // send buffer
                            {
                                byte[] dataToSend;
                                lock (_sendBufferLock)
                                {
                                    dataToSend = _encoding.GetBytes(_sendBuffer.ToString());
                                    _sendBuffer.Clear();
                                }

                                int pos = 0;
                                int sent;
                                while (pos < dataToSend.Length)
                                {
                                    try
                                    {
                                        sent = socket.Send(dataToSend, pos, dataToSend.Length - pos, SocketFlags.None);
                                        pos += sent;
                                    }
                                    catch (Exception ex)
                                    {
                                        OnException(ex);
                                        throw new ExitClientLoopException();
                                    }
                                }
                            }
                            else if (n == 1) // shutdown
                            {
                                OnDisconnected();
                                throw new ExitClientLoopException();
                            }

                            if (now >= nextSendPing)
                            {
                                Send("|"); // basic ping
                                nextSendPing = now.AddMinutes(2);
                            }

                            len = 0;

                            try
                            {
                                poll = socket.Poll(1, SelectMode.SelectRead);
                            }
                            catch (Exception ex)
                            {
                                OnException(ex);
                                throw new ExitClientLoopException();
                            }

                            if (poll)
                            {
                                try
                                {
                                    len = socket.Receive(buffer);

                                    if (len <= 0)
                                        throw new SocketException((int)SocketError.NotConnected);
                                }
                                catch (Exception ex)
                                {
                                    OnException(ex);
                                    throw new ExitClientLoopException();
                                }
                            }

                            if (len > 0)
                            {
                                try
                                {
                                    ParseProtocolData(buffer, 0, len);
                                }
                                catch (ParseDataException ex)
                                {
                                    OnException(ex);
                                    throw new ExitClientLoopException();
                                }
                            }
                        }
                    }
                }
                catch (ExitClientLoopException)
                {
                    // allows to safely exit out of any nested while loops, to reset the client
                }
            }
        }

        protected virtual void ParseProtocolData(byte[] buffer, int offset, int length)
        {
            for (var i = 0; i < length; ++i)
            {
                if (buffer[offset + i] == '|')
                {
                    // combine previous data into below packet
                    var packet = _buffer.ToString() + _encoding.GetString(buffer, offset, i);
                    _buffer.Clear();

                    if (packet.Length > 0)
                        ParsePacket(packet);

                    if (i + 1 < length)
                        ParseProtocolData(buffer, i + 1, length - (i + 1)); // recursive on the rest of the data (if any)

                    return;
                }
            }

            _buffer.Append(_encoding.GetString(buffer, offset, length));
        }

        protected virtual void ParsePacket(string packet)
        {
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

            if (header == "new") // new instance info
            {
                var data = content.Split(':');
                if (data.Length != 2)
                    throw new ParseDataException("Invalid NEW packet from service.");

                OnNewInstance(data[0], Utilities.DecodeAsciiString(data[1]));
            }
            else if (header == "is") // instance status
            {
                var data = content.Split(' ');
                int instanceStatus;
                if (data.Length != 2 ||
                    !int.TryParse(data[1], out instanceStatus))
                    throw new ParseDataException("Invalid IS packet from service.");

                OnInstanceStatus(data[0], (InstanceStatus)instanceStatus);
            }
            else if (header == "il") // instance log
            {
                if ((pos = content.IndexOf(' ')) == -1)
                    throw new ParseDataException("Invalid IL packet from service.");

                OnInstanceLog(
                    content.Substring(0, pos),
                    Utilities.DecodeAsciiString(content.Substring(pos + 1)));
            }
            else if (header == "cfg")
            {
                if ((pos = content.IndexOf(':')) == -1)
                    throw new ParseDataException("Invalid CFG packet from service.");

                var instanceId = content.Substring(0, pos);
                InstanceConfiguration configuration;

                try
                {
                    configuration = InstanceConfiguration.Deserialize(content.Substring(pos + 1));
                }
                catch
                {
                    throw new ParseDataException("Failed to deserialize instance configuration in CFG packet: " + content);
                }

                OnInstanceConfiguration(instanceId, configuration);
            }
            else if (header == "lst")
            {
                var instances = new List<InstanceInformation>();

                var list = content.Split(new char[] { '$' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var item in list)
                {
                    var data = item.Split(':');
                    if (data.Length != 3)
                        throw new ParseDataException("Invalid LST packet from service.");

                    instances.Add(new InstanceInformation(
                        data[0],
                        (InstanceStatus)int.Parse(data[1]),
                        Utilities.DecodeAsciiString(data[2])));
                }

                OnInstanceList(instances);
            }
            else if (header == "err") // error message
            {
                OnServiceError(Utilities.DecodeAsciiString(content));
            }
        }

        #region Network events
        protected virtual void OnConnecting()
        {
        }

        protected virtual void OnConnected()
        {
        }

        protected virtual void OnConnectionFailed(Exception ex)
        {
        }

        protected virtual void OnDisconnected()
        {
        }

        protected virtual void OnException(Exception ex)
        {
        }
        #endregion

        #region Service events
        protected virtual void OnNewInstance(string instanceId, string packageName)
        {
        }

        protected virtual void OnInstanceStatus(string instanceId, InstanceStatus status)
        {
        }

        protected virtual void OnInstanceLog(string instanceId, string text)
        {
        }

        protected virtual void OnInstanceConfiguration(string instanceId, InstanceConfiguration configuration)
        {
        }

        protected virtual void OnInstanceList(IEnumerable<InstanceInformation> instances)
        {
        }

        protected virtual void OnServiceError(string message)
        {
        }
        #endregion
    }

    public class ParseDataException : Exception
    {
        public ParseDataException(string message) :
            base(message)
        {
        }
    }

    public class ExitClientLoopException : Exception
    {
    }
}
