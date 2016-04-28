using MCHost.Framework.Minecraft;
using MCHost.Framework.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;

namespace MCHost.Web
{
    public interface ILogItem
    {
        long Id { get; }
        string Text { get; }
    }

    public interface IRequestChain
    {
        int Id { get; }
    }

    public interface IRequestIdAllocator
    {
        int AllocateRequestId(IRequestChain requestChain);
        void FreeRequestId(int requestId);
    }

    public interface IHostClient
    {
        IEnumerable<ILogItem> GetLog(long lastLogId);
        IEnumerable<ILogItem> GetLog();
        string CreateInstance(string packageName, InstanceConfiguration configuration);
        IEnumerable<InstanceInformation> GetInstances();
    }

    public class HostClient : NetworkClient, IHostClient, IRequestIdAllocator
    {
        class LogItem : ILogItem
        {
            public long Id { get; private set; }
            public string Text { get; private set; }

            public LogItem(long id, string text)
            {
                Id = id;
                Text = text;
            }
        }

        class RequestChain : IDisposable, IRequestChain
        {
            public int Id { get; private set; }

            private readonly IRequestIdAllocator _requestIdAllocator;
            private readonly ManualResetEvent _completedEvent = new ManualResetEvent(false);
            private readonly ManualResetEvent _cancelEvent = new ManualResetEvent(false);

            public Header ResultHeader { get; set; }
            public DataBuffer ResultBuffer { get; set; }

            public RequestChain(IRequestIdAllocator requestIdAllocator)
            {
                _requestIdAllocator = requestIdAllocator;
                Id = _requestIdAllocator.AllocateRequestId(this);
            }

            public void Dispose()
            {
                _requestIdAllocator.FreeRequestId(Id);
                _completedEvent.Dispose();
                _cancelEvent.Dispose();
            }

            /// <summary>
            /// Waits for the request to finish.
            /// </summary>
            /// <param name="milliseconds">Amount of milliseconds to wait before timing out.</param>
            /// <exception cref="RequestChainTimeoutException"></exception>
            /// <exception cref="RequestChainCanceledException"></exception>
            public void WaitForResult(int milliseconds)
            {
                var events = new WaitHandle[] { _completedEvent, _cancelEvent };

                switch (WaitHandle.WaitAny(events, milliseconds))
                {
                    case 0: // completed
                        break;
                    case 1:
                        throw new RequestChainCanceledException();
                    case WaitHandle.WaitTimeout:
                        throw new RequestChainTimeoutException();
                }
            }

            public void Complete()
            {
                _completedEvent.Set();
            }
        }

        class RequestChainTimeoutException : Exception { }
        class RequestChainCanceledException : Exception { }

        private readonly List<LogItem> _log = new List<LogItem>();
        private long _nextLogId = 1;
        private readonly object _lock = new object();

        private readonly Queue<int> _requestIdQueue = new Queue<int>();
        private int _nextRequestId = 1;
        private readonly Dictionary<int, RequestChain> _requestChains = new Dictionary<int, RequestChain>();
        private readonly object _requestChainLock = new object();

        private readonly Dictionary<string, InstanceInformation> _runningInstances = new Dictionary<string, InstanceInformation>();

        private void Log(string str)
        {
            lock (_lock)
            {
                _log.Add(new LogItem(_nextLogId++, str));
            }
        }

        public IEnumerable<ILogItem> GetLog(long lastLogId)
        {
            var list = new List<ILogItem>();

            lock (_lock)
            {
                foreach (var item in _log.Where((a) => a.Id > lastLogId))
                {
                    list.Add(item);
                }
            }

            return list;
        }

        public IEnumerable<ILogItem> GetLog()
        {
            return GetLog(0);
        }

        #region Network events
        protected override void OnConnecting()
        {
            Log("Connecting ...");
        }

        protected override void OnConnected()
        {
            Log("Connected to server.");
        }

        protected override void OnConnectionFailed(Exception ex)
        {
            Log($"Connection failed. ({ex.GetType().Name}) {ex.Message}");
        }

        protected override void OnDisconnected()
        {
            Log("Disconnected.");
        }

        protected override void OnException(Exception ex)
        {
            Log($"({ex.GetType().Name}) {ex.Message}");
        }
        #endregion

        #region Service events
        protected override void OnNewInstance(int requestId, string instanceId, string packageName)
        {
            Log($"New instance of package '{packageName}' => {instanceId}");
        }

        protected override void OnInstanceStatus(int requestId, string instanceId, InstanceStatus status)
        {
            Log($"[{instanceId}] Status changed to " + status.ToString());
        }

        protected override void OnInstanceLog(int requestId, string instanceId, string text)
        {
            Log($"[{instanceId}] {text}");
        }

        protected override void OnInstanceConfiguration(int requestId, string instanceId, InstanceConfiguration configuration)
        {
            Log($"[{instanceId}] Received config.");
        }

        protected override void OnInstanceList(int requestId, IEnumerable<InstanceInformation> instances)
        {
            var sb = new StringBuilder();

            sb.Append($"{instances.Count()} instance(s) running:");

            foreach (var instance in instances)
            {
                sb.Append($"\r\n- {instance.Id} - {instance.Status} - Package: {instance.PackageName}");
            }

            Log(sb.ToString());
        }

        protected override void OnServiceError(int requestId, string message)
        {
            Log($"[SERVICE ERROR] {message}");
        }
        #endregion

        protected override bool PreProcessPacket(Header header, int requestId, int packetSize, DataBuffer buffer)
        {
            lock (_requestChainLock)
            {
                RequestChain requestChain;
                if (_requestChains.TryGetValue(requestId, out requestChain))
                {
                    var newBuffer = new DataBuffer();
                    newBuffer.Write(buffer.InternalBuffer, buffer.Offset, packetSize);
                    newBuffer.Offset = 0;

                    requestChain.ResultHeader = header;
                    requestChain.ResultBuffer = newBuffer;
                    requestChain.Complete();
                    return false; // don't further process this packet since it was consumed by a request chain
                }
            }

            return base.PreProcessPacket(header, requestId, packetSize, buffer);
        }

        public int AllocateRequestId(IRequestChain requestChain)
        {
            lock (_requestChainLock)
            {
                if (_requestIdQueue.Count == 0)
                {
                    for (int i = 0; i < 32; ++i)
                        _requestIdQueue.Enqueue(_nextRequestId++);
                }

                var id = _requestIdQueue.Dequeue();
                _requestChains.Add(id, (RequestChain)requestChain);
                return id;
            }
        }

        public void FreeRequestId(int requestId)
        {
            lock (_requestChainLock)
            {
                _requestChains.Remove(requestId);
                _requestIdQueue.Enqueue(requestId);
            }
        }

        public string CreateInstance(string packageName, InstanceConfiguration configuration)
        {
            using (var requestChain = new RequestChain(this))
            {
                var packet = new Packet(Header.New, requestChain.Id);
                packet.Write(packageName);
                configuration.Serialize(packet);
                Send(packet);

                requestChain.WaitForResult(5000);

                return requestChain.ResultBuffer.ReadString(); // instanceId
            }
        }

        public IEnumerable<InstanceInformation> GetInstances()
        {
            using (var requestChain = new RequestChain(this))
            {
                Send(new Packet(Header.List, requestChain.Id));

                requestChain.WaitForResult(5000);

                var list = new List<InstanceInformation>();

                var buffer = requestChain.ResultBuffer;
                var numberOfInstances = buffer.ReadInt32();
                while (numberOfInstances-- > 0)
                {
                    var instanceId = buffer.ReadString();
                    var status = (InstanceStatus)buffer.ReadInt32();
                    var packageName = buffer.ReadString();
                    var configuration = InstanceConfiguration.Deserialize(buffer);

                    list.Add(new InstanceInformation(instanceId, status, packageName, configuration));
                }

                return list;
            }
        }
    }
}