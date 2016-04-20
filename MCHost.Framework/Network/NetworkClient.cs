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
        private DateTime? _reconnectTime = null;
        private readonly object _lock = new object();

        private readonly DataBuffer _buffer = new DataBuffer();

        private readonly DataBuffer _sendBuffer = new DataBuffer();
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

        public void Send(Packet packet)
        {
            lock (_sendBufferLock)
            {
                _sendBuffer.Offset = _sendBuffer.Length;
                packet.WriteTo(_sendBuffer);
                _sendBufferEvent.Set();
            }
        }

        public void SendHeader(Header header, int requestId)
        {
            lock (_sendBufferLock)
            {
                _sendBuffer.Offset = _sendBuffer.Length;
                _sendBuffer.Write((int)header);
                _sendBuffer.Write(requestId);
                _sendBuffer.Write(0); // packet size
                _sendBufferEvent.Set();
            }
        }

        private void WorkerThread()
        {
            var events = new WaitHandle[] { _connectEvent, _shutdownEvent };

            while (true)
            {
                int n = WaitHandle.WaitAny(events, 100);
                if (n == 1) // shutdown
                    break;

                if (n == WaitHandle.WaitTimeout)
                {
                    var now = DateTime.UtcNow;
                    if (_reconnectTime.HasValue &&
                        now >= _reconnectTime.Value)
                        _connectEvent.Set();
                    continue;
                }

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
                            throw new ExitClientLoopException(TimeSpan.FromSeconds(30));
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
                                    dataToSend = new byte[_sendBuffer.Length];
                                    Buffer.BlockCopy(_sendBuffer.InternalBuffer, 0, dataToSend, 0, _sendBuffer.Length);
                                    _sendBuffer.Reset();
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
                                SendHeader(Header.Ping, Packet.NoRequestId);
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
                                    DecodeProtocolData(buffer, 0, len);
                                }
                                catch (ProtocolException ex)
                                {
                                    OnException(ex);
                                    throw new ExitClientLoopException();
                                }
                                catch (EndOfBufferException ex)
                                {
                                    OnException(ex);
                                    throw new ExitClientLoopException();
                                }
                            }
                        }
                    }
                }
                catch (ExitClientLoopException ex)
                {
                    _reconnectTime = DateTime.UtcNow + ex.ReconnectTime;
                    // allows to safely exit out of any nested while loops, to reset the client
                }
            }
        }

        protected virtual void DecodeProtocolData(byte[] buffer, int offset, int length)
        {
            _buffer.Offset = _buffer.Length;
            _buffer.Write(buffer, offset, length);

            while (_buffer.Length >= Packet.HeaderSize)
            {
                _buffer.Offset = 0;
                var header = _buffer.ReadInt32();
                var requestId = _buffer.ReadInt32();
                var packetSize = _buffer.ReadInt32();

                if (_buffer.Length - _buffer.Offset < packetSize)
                    return; // need more data

                if (header <= 0 ||
                    header >= (int)Header.MaxHeader)
                    throw new ProtocolException($"The header is invalid: 0x{header.ToString("x8")}");

                if (PreProcessPacket((Header)header, requestId, packetSize, _buffer))
                    ProcessPacket((Header)header, requestId);

                _buffer.Remove(Packet.HeaderSize + packetSize);
            }
        }

        protected virtual bool PreProcessPacket(Header header, int requestId, int packetSize, DataBuffer buffer)
        {
            return true;
        }

        protected virtual void ProcessPacket(Header header, int requestId)
        {
            if (header == Header.New) // new instance info
            {
                var instanceId = _buffer.ReadString();
                var packageName = _buffer.ReadString();

                OnNewInstance(requestId, instanceId, packageName);
            }
            else if (header == Header.InstanceStatus) // instance status
            {
                var instanceId = _buffer.ReadString();
                var status = (InstanceStatus)_buffer.ReadInt32();

                OnInstanceStatus(requestId, instanceId, status);
            }
            else if (header == Header.InstanceLog) // instance log
            {
                var instanceId = _buffer.ReadString();
                var text = _buffer.ReadString();

                OnInstanceLog(requestId, instanceId, text);
            }
            else if (header == Header.InstanceConfiguration)
            {
                var instanceId = _buffer.ReadString();
                var configuration = InstanceConfiguration.Deserialize(_buffer);

                OnInstanceConfiguration(requestId, instanceId, configuration);
            }
            else if (header == Header.List)
            {
                var numberOfInstances = _buffer.ReadInt32();
                var instances = new List<InstanceInformation>(numberOfInstances);

                while (numberOfInstances-- > 0)
                {
                    var instanceId = _buffer.ReadString();
                    var status = (InstanceStatus)_buffer.ReadInt32();
                    var packageName = _buffer.ReadString();
                    var configuration = InstanceConfiguration.Deserialize(_buffer);

                    instances.Add(new InstanceInformation(instanceId, status, packageName, configuration));
                }

                OnInstanceList(requestId, instances);
            }
            else if (header == Header.Error) // error message
            {
                var text = _buffer.ReadString();

                OnServiceError(requestId, text);
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
        protected virtual void OnNewInstance(int requestId, string instanceId, string packageName)
        {
        }

        protected virtual void OnInstanceStatus(int requestId, string instanceId, InstanceStatus status)
        {
        }

        protected virtual void OnInstanceLog(int requestId, string instanceId, string text)
        {
        }

        protected virtual void OnInstanceConfiguration(int requestId, string instanceId, InstanceConfiguration configuration)
        {
        }

        protected virtual void OnInstanceList(int requestId, IEnumerable<InstanceInformation> instances)
        {
        }

        protected virtual void OnServiceError(int requestId, string message)
        {
        }
        #endregion
    }

    public class ProtocolException : Exception
    {
        public ProtocolException(string message) :
            base(message)
        {
        }
    }

    public class ExitClientLoopException : Exception
    {
        public TimeSpan ReconnectTime { get; private set; }

        public ExitClientLoopException()
        {
            ReconnectTime = TimeSpan.FromSeconds(5);
        }

        public ExitClientLoopException(TimeSpan reconnectTime)
        {
            ReconnectTime = reconnectTime;
        }
    }
}
