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

        public static User FromResult(QueryResult result)
        {
            var i = 0;
            return new User()
            {
                Id = result.GetInt32(i++),
                Email = result.GetString(i++),
                DisplayName = result.GetString(i++),
                Role = (AccountRole)result.GetInt32(i++)
            };
        }
    }
}
