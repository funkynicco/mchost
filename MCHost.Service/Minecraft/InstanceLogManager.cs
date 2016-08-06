using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Service.Minecraft
{
    public interface IInstanceLogItem
    {
        DateTime Time { get; }
        string Text { get; }
    }

    public static class InstanceLogManager
    {
        struct InstanceLogItem : IInstanceLogItem
        {
            public DateTime Time { get; private set; }
            public string Text { get; private set; }

            public InstanceLogItem(DateTime time, string text)
            {
                Time = time;
                Text = text;
            }
        }

        class InstanceLog
        {
            public const int MaxLogItems = 2000;

            private readonly LinkedList<IInstanceLogItem> _log = new LinkedList<IInstanceLogItem>();

            public string InstanceId { get; private set; }
            public IEnumerable<IInstanceLogItem> Log { get { return _log; } }

            public InstanceLog(string instanceId)
            {
                InstanceId = instanceId;
            }

            public void Add(string text)
            {
                _log.AddLast(new InstanceLogItem(DateTime.UtcNow, text));

                while (_log.Count > MaxLogItems)
                {
                    _log.RemoveFirst();
                }
            }

            public IEnumerable<IInstanceLogItem> GetLast(int max)
            {
                var last = _log.Last;

                var items = new IInstanceLogItem[Math.Min(max, _log.Count)];
                for (int i = items.Length - 1; i >= 0; --i)
                {
                    items[i] = last.Value;
                    last = last.Previous;
                }

                return items;
            }
        }

        private static readonly Dictionary<string, InstanceLog> _log = new Dictionary<string, InstanceLog>();
        private static readonly object _lock = new object();

        public static void AddLog(string instanceId, string text)
        {
            lock (_lock)
            {
                InstanceLog log;
                if (!_log.TryGetValue(instanceId, out log))
                    _log.Add(instanceId, log = new InstanceLog(instanceId));

                log.Add(text);
            }
        }

        public static bool Remove(string instanceId)
        {
            lock (_lock)
            {
                return _log.Remove(instanceId);
            }
        }

        public static IEnumerable<IInstanceLogItem> GetLast(string instanceId, int max)
        {
            lock (_lock)
            {
                InstanceLog log;
                if (!_log.TryGetValue(instanceId, out log))
                    return new List<IInstanceLogItem>();

                return log.GetLast(max);
            }
        }
    }
}
