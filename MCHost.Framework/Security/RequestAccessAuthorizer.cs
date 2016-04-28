using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace MCHost.Framework.Security
{
    public static class RequestAccessAuthorizer
    {
        public static User Authorize(IDatabase database)
        {
            User user = null;
            string sessionKey = "";

            var cookie = HttpContext.Current.Request.Cookies[UserCookie.CookieName];
            if (cookie != null)
            {
                int userid;
                DateTime expires;
                if (UserCookieCache.ResumeSession(cookie.Value, HttpContext.Current.Request.UserHostAddress, out userid, out expires))
                {
                    if ((user = database.GetUser(userid)) != null)
                    {
                        sessionKey = cookie.Value;

                        // Update cookie
                        cookie = new HttpCookie(UserCookie.CookieName);
                        cookie.Expires = expires;
                        cookie.Value = sessionKey;
                        if (!string.IsNullOrEmpty(UserCookie.CookieDomain))
                            cookie.Domain = UserCookie.CookieDomain;
                        HttpContext.Current.Response.Cookies.Add(cookie);
                    }
                    else
                    {
                        UserCookieCache.DestroySession(sessionKey);
                        database.DeleteUserCookies(user.Id);
                        database.AddUserLog(user.Id, "Logged out");

                        // remove cookie
                        cookie = new HttpCookie(UserCookie.CookieName);
                        cookie.Expires = DateTime.UtcNow.AddDays(-1);
                        if (!string.IsNullOrEmpty(UserCookie.CookieDomain))
                            cookie.Domain = UserCookie.CookieDomain;
                        HttpContext.Current.Response.Cookies.Add(cookie);
                    }
                }
            }

            HttpContext.Current.User = new GenericPrincipal(new UserIdentity(user, sessionKey), new string[] { });

            return user;
        }
    }
}
