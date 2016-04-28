using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Security
{
    public class UserCookie
    {
        public const string CookieName = "mch_2ju543hunac";
#if DEBUG
        public static readonly string CookieDomain = null; // do not use any domain for localhost
#else
        public static readonly string CookieDomain = ConfigurationManager.AppSettings["CookieDomain"];
#endif

        private string _email = "";
        public string Email
        {
            get { return _email; }
            set
            {
                _email = value;
                UpdateHash();
            }
        }

        private int _randomKey = 0;
        public int RandomKey
        {
            get { return _randomKey; }
            set
            {
                _randomKey = value;
                UpdateHash();
            }
        }

        public string SecureHash { get; private set; }

        public UserCookie(string email, int randomKey)
        {
            _email = email;
            _randomKey = randomKey;
            UpdateHash();
        }

        private void UpdateHash()
        {
            using (var hash = SHA512.Create())
            {
                var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(_email + "_" + _randomKey));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes)
                    sb.Append(b.ToString("X2"));
                SecureHash = sb.ToString();
            }
        }

        public static UserCookie Create(string email)
        {
            return new UserCookie(email, new Random(Environment.TickCount).Next());
        }
    }
}
