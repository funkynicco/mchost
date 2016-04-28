using MCHost.Framework;
using MCHost.Framework.MVC;
using MCHost.Framework.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MCHost.Web.Controllers
{
    public class AccountController : BaseController
    {
        private readonly IDatabase _database;

        public AccountController(IDatabase database) :
            base(database)
        {
            _database = database;
        }

        public ActionResult Logout()
        {
            if (!IsAuthenticated)
                return Redirect("/");

            var user_id = CurrentUser.Id;

            UserCookieCache.DestroySession((User.Identity as UserIdentity).SessionKey);

            var cookie = new HttpCookie(UserCookie.CookieName);
            cookie.Expires = DateTime.UtcNow.AddDays(-1);
            cookie.Value = "";
            if (!string.IsNullOrEmpty(UserCookie.CookieDomain))
                cookie.Domain = UserCookie.CookieDomain;
            Response.Cookies.Add(cookie);

            _database.AddUserLog(user_id, "Logged out");

            return Redirect("/");
        }
    }
}