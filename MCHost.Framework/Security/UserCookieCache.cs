using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Security
{
    public class UserCookieCache
    {
        class DummyLog : ILogger
        {
            public void Write(LogType type, string message)
            {
            }

            public void Write(LogType type, string format, params object[] args)
            {
            }
        }

        public static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(30);
        private static Dictionary<string, UserCookieCacheItem> _cookies = new Dictionary<string, UserCookieCacheItem>();
        private static object _lock = new object();
        private static readonly IDatabase _database;

        static UserCookieCache()
        {
            // Load cookie cache from SQL on application pool restart
            _database = new Database(new Settings());
            _database.LoadUserCookieCache(ref _cookies);
        }

        // mirror all changes in SQL
        public static bool ResumeSession(string key, string ip, out int userid, out DateTime expireDate)
        {
            lock (_lock)
            {
                userid = 0;
                expireDate = new DateTime(0);
                UserCookieCacheItem item;

                if (_cookies.TryGetValue(key, out item) &&
                    item.IP == ip &&
                    DateTime.UtcNow < item.ExpireDate)
                {
                    item.ExpireDate = DateTime.UtcNow + CookieLifetime;
                    userid = item.UserId;

                    // update SQL
                    DateTime expires = item.ExpireDate;
                    expireDate = expires;
                    AsyncTask.Run(() => _database.UpdateUserCookieExpireDate(key, expires));

                    return true;
                }

                return false;
            }
        }

        public static void AddSession(string key, string ip, int userid, DateTime expireDate)
        {
            AsyncTask.Run(() => _database.AddUserCookie(key, ip, userid, expireDate));

            lock (_lock)
                _cookies[key] = new UserCookieCacheItem(key, ip, userid, expireDate);
        }

        public static void DestroySession(string key)
        {
            AsyncTask.Run(() => _database.DeleteUserCookie(key));

            lock (_lock)
            {
                if (_cookies.ContainsKey(key))
                    _cookies.Remove(key);
            }
        }
    }
}
