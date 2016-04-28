using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace MCHost.Framework.Security
{
    /// <summary>
    /// Restricts access to the MVC Controller or MVC Controller method.
    /// <para>For Web API 2, use [RestrictApi].</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RestrictAttribute : AuthorizeAttribute
    {
        private readonly AccountRole _minimumRole = AccountRole.User;
        
        public RestrictAttribute(AccountRole minimumRole)
        {
            _minimumRole = minimumRole;
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (_minimumRole > AccountRole.User) // Required role must be set above a user that is not logged in before performing the checks below
            {
                if (httpContext == null) // Safety check
                    return false;

                if (httpContext.User == null) // Safety check
                {
                    httpContext.Response.Redirect("/", true);
                    return false;
                }

                if (httpContext.User.Identity == null) // Safety check
                {
                    httpContext.Response.Redirect("/", true);
                    return false;
                }

                if ((httpContext.User.Identity as UserIdentity) == null) // conversion failed check (the User.Identity is not of UserIdentity)
                {
                    httpContext.Response.Redirect("/", true);
                    return false;
                }

                var user = ((UserIdentity)httpContext.User.Identity).User;

                if (user == null) // not logged in
                {
                    httpContext.Response.Redirect("/login", true);
                    return false;
                }

                if (user.Role < _minimumRole) // account doesnt have privileges
                {
                    httpContext.Response.ContentType = "text/html";
                    httpContext.Response.Write("<h2>Unauthorized</h2><p>You do not have the required privileges to access this resource.</p>");
                    httpContext.Response.End();
                    return false;
                }
            }

            return true;
        }
    }
}
