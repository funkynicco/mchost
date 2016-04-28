using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Security
{
    public class UserCookieCacheItem
    {
        public string Key { get; private set; }
        public string IP { get; private set; }
        public int UserId { get; private set; }
        public DateTime ExpireDate { get; set; }

        public UserCookieCacheItem()
        {
        }

        public UserCookieCacheItem(string key, string ip, int userid, DateTime expireDate)
        {
            Key = key;
            IP = ip;
            UserId = userid;
            ExpireDate = expireDate;
        }

        public static UserCookieCacheItem FromResult(QueryResult result)
        {
            int i = 0;
            return new UserCookieCacheItem()
            {
                Key = result.GetString(i++),
                IP = result.GetString(i++),
                UserId = result.GetInt32(i++),
                ExpireDate = result.GetUtcDateTime(i++).Value
            };
        }
    }
}
