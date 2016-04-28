using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Security
{
    public static class BruteForceLock
    {
        private class IPLock
        {
            public string IP { get; private set; }
            public DateTime BanExpireDate { get; set; }
            public int Attempts { get; set; }
            public int BanTimes { get; set; }

            public IPLock(string ip)
            {
                IP = ip;
                BanExpireDate = new DateTime(0);
                Attempts = 0;
            }
        }

        private static readonly TimeSpan StageBanTime = TimeSpan.FromMinutes(5);
        private const int MaxAttempts = 3; // max amount of attempts before starting to block the user's IP for a period of time
        private const int UpperBanLimit = 15; // max amount of stages of block time => StageBanTime * (NumberOfAttempts - MaxAttempts)

        private static object locker = new object();
        private static Dictionary<string, IPLock> iplocks = new Dictionary<string, IPLock>();

        public static bool IsBanned(string ip, out TimeSpan ts)
        {
            lock (locker)
            {
                IPLock iplock;
                if (iplocks.TryGetValue(ip, out iplock) &&
                    iplock.BanExpireDate > DateTime.UtcNow)
                {
                    ts = iplock.BanExpireDate - DateTime.UtcNow;
                    return true;
                }

                ts = new TimeSpan(0);
                return false;
            }
        }

        public static bool IsBanned(string ip)
        {
            TimeSpan ts;
            return IsBanned(ip, out ts);
        }

        public static int GetRemainingAttempts(string ip)
        {
            lock (locker)
            {
                IPLock iplock;
                if (iplocks.TryGetValue(ip, out iplock))
                    return (MaxAttempts + iplock.BanTimes) - iplock.Attempts;
            }

            return MaxAttempts;
        }

        public static TimeSpan? OnFailed(string ip)
        {
            TimeSpan? ts = null;
            IPLock iplock;

            lock (locker)
            {
                if (!iplocks.TryGetValue(ip, out iplock))
                {
                    iplock = new IPLock(ip);
                    iplocks[ip] = iplock;
                }

                if (++iplock.Attempts >= MaxAttempts) // only ban after MaxAttempts has been exceeded (default 3)
                {
                    ++iplock.BanTimes;
                    // this will multiply the time every repeated attempt to login up to UpperBanLimit times
                    // by default the max ban time is 5 * 15 = 
                    var timeMultiplier = Math.Min(UpperBanLimit, (iplock.Attempts - MaxAttempts) + 1);

                    iplock.BanExpireDate = DateTime.UtcNow.AddMinutes(5 * timeMultiplier);
                    ts = iplock.BanExpireDate - DateTime.UtcNow;
                }
            }

            return ts;
        }

        public static void OnSuccess(string ip)
        {
            lock (locker)
            {
                iplocks.Remove(ip);
            }
        }

        public static void Clear()
        {
            lock (locker)
            {
                iplocks.Clear();
            }
        }
    }
}
