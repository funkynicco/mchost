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

    public class HostClient : NetworkClient
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

        private readonly List<LogItem> _log = new List<LogItem>();
        private long _nextLogId = 1;
        private readonly object _lock = new object();

        private readonly object _dataResultLock = new object();
        private readonly ManualResetEvent _dataResultEvent = new ManualResetEvent(false);
        private string _dataResultHeaderSubscribe = null;
        private object[] _dataResultParameters = null;

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

        protected override void ParsePacket(string packet)
        {
            //Log("[RAW] " + packet);
            base.ParsePacket(packet);
        }

        #region Network events
        protected override void OnConnecting()
        {
            Log("Connecting ...");
        }

        protected override void OnConnected()
        {
            Log("Connected to server.");
            Send("LST|"); // get running instances
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
        protected override void OnNewInstance(string instanceId, string packageName)
        {
            Log($"New instance of package '{packageName}' => {instanceId}");

            lock (_dataResultLock)
            {
                if (_dataResultHeaderSubscribe == "new")
                {
                    _dataResultParameters = new object[] { instanceId, packageName };
                    _dataResultEvent.Set();
                }
            }
        }

        protected override void OnInstanceStatus(string instanceId, InstanceStatus status)
        {
            Log($"[{instanceId}] Status changed to " + status.ToString());

            lock (_dataResultLock)
            {
                if (_dataResultHeaderSubscribe == "is")
                {
                    _dataResultParameters = new object[] { instanceId, status };
                    _dataResultEvent.Set();
                }
            }
        }

        protected override void OnInstanceLog(string instanceId, string text)
        {
            Log($"[{instanceId}] {text}");

            lock (_dataResultLock)
            {
                if (_dataResultHeaderSubscribe == "il")
                {
                    _dataResultParameters = new object[] { instanceId, text };
                    _dataResultEvent.Set();
                }
            }
        }

        protected override void OnInstanceConfiguration(string instanceId, InstanceConfiguration configuration)
        {
            Log($"[{instanceId}] Config: " + configuration.Serialize());
        }

        protected override void OnInstanceList(IEnumerable<InstanceInformation> instances)
        {
            var sb = new StringBuilder();

            sb.Append($"{instances.Count()} instance(s) running:");

            foreach (var instance in instances)
            {
                sb.Append($"\r\n- {instance.Id} - {instance.Status} - Package: {instance.PackageName}");
            }

            Log(sb.ToString());
        }

        protected override void OnServiceError(string message)
        {
            Log($"[SERVICE ERROR] {message}");
        }
        #endregion

        public void CreateInstance()
        {
            // create subscription to a packet response and send the request with the subscription id/key
            // wait for result
            // return the result data ...

            // var subscription = CreateSubscription("cfg");
            // Send("cfg", "data of packet", subscription.Id);
            // subscription.WaitForResult();
            // return subscription.Result[0]; // instance id
        }
    }
}