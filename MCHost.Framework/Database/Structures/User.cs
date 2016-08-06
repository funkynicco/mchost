using MCHost.Framework.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework
{
    public class User
    {
        public int Id { get; private set; }
        public string Email { get; private set; }
        public string DisplayName { get; private set; }
        public AccountRole Role { get; private set; }
        public string Timezone { get; private set; }

        private TimeZoneInfo _timezoneInfo = null;
        public TimeZoneInfo TimezoneInfo
        {
            get
            {
                if (_timezoneInfo == null &&
                    Timezone != null)
                    _timezoneInfo = TimeZoneInfo.FindSystemTimeZoneById(Timezone);

                return _timezoneInfo;
            }
        }

        public DateTime GetLocalDateTime(DateTime date, DateTimeKind kind = DateTimeKind.Utc)
        {
            if (kind == DateTimeKind.Local)
                date = date.ToUniversalTime();

            var tzi = TimezoneInfo;
            if (tzi != null)
                date = TimeZoneInfo.ConvertTime(date, tzi);

            return date;
        }

        public static User FromResult(QueryResult result)
        {
            var i = 0;
            return new User()
            {
                Id = result.GetInt32(i++),
                Email = result.GetString(i++),
                DisplayName = result.GetString(i++),
                Role = (AccountRole)result.GetInt32(i++),
                Timezone = result.GetString(i++)
            };
        }
    }
}
