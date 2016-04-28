using MCHost.Framework;
using MCHost.Framework.MVC;
using MCHost.Framework.Security;
using MCHost.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;

namespace MCHost.Web.Controllers
{
    public class LoginController : BaseApiController
    {
        private readonly IDatabase _database;

        public LoginController(IDatabase database) :
            base(database)
        {
            _database = database;
        }

        private int GetRemainingAttempts()
        {
            return BruteForceLock.GetRemainingAttempts(HttpContext.Current.Request.UserHostAddress);
        }

        public object Get()
        {
            TimeSpan ts;
            if (BruteForceLock.IsBanned(HttpContext.Current.Request.UserHostAddress, out ts))
            {
                return new
                {
                    attempts = 0,
                    banTime = (int)ts.TotalSeconds
                };
            }

            return new { attempts = GetRemainingAttempts() };
        }

        public object Post([FromBody] LoginModel model)
        {
            if (IsAuthenticated)
                return new { result = false };

            if (BruteForceLock.IsBanned(HttpContext.Current.Request.UserHostAddress))
                return new { result = false, attempts = 0 };

            if (!ModelState.IsValid)
            {
                var errors = new List<string>();
                foreach (var value in ModelState.Values)
                {
                    foreach (var error in value.Errors)
                    {
                        errors.Add(error.ErrorMessage);
                    }
                }

                return new { result = false, attempts = GetRemainingAttempts(), errors = errors };
            }

            var email = model.Email.ToLower();
            var password_hash = model.Password.ToPasswordHash(email);

            var user = _database.Login(email, password_hash);
            if (user == null)
            {
                var ts = BruteForceLock.OnFailed(HttpContext.Current.Request.UserHostAddress);
                if (ts.HasValue)
                {
                    return new
                    {
                        result = false,
                        attempts = 0,
                        banTime = (int)ts.Value.TotalSeconds
                    };
                }

                // username or password wrong
                return new
                {
                    result = false,
                    attempts = GetRemainingAttempts()
                };
            }

            // set cookie etc...
            BruteForceLock.OnSuccess(HttpContext.Current.Request.UserHostAddress);

            var userCookie = UserCookie.Create(email);
            var expireDate = DateTime.UtcNow + UserCookieCache.CookieLifetime;

            var cookie = new HttpCookie(UserCookie.CookieName);
            cookie.Expires = expireDate;
            cookie.Value = userCookie.SecureHash;
            if (!string.IsNullOrWhiteSpace(UserCookie.CookieDomain))
                cookie.Domain = UserCookie.CookieDomain;
            HttpContext.Current.Response.Cookies.Add(cookie);

            UserCookieCache.AddSession(userCookie.SecureHash, HttpContext.Current.Request.UserHostAddress, user.Id, expireDate);
            _database.AddUserLog(user.Id, "Logged in");

            return new { result = true };
        }
    }
}