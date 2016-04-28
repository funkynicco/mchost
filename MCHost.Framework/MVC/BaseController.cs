using MCHost.Framework.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace MCHost.Framework.MVC
{
    public class BaseController : Controller
    {
        /// <summary>
        /// Gets or sets the default cache duration for objects in this controller.
        /// </summary>
        protected TimeSpan CacheDuration { get; set; }

        protected virtual bool RequireAccount { get; }

        /// <summary>
        /// [Nicco] Gets the currently logged on user.
        /// </summary>
        protected User CurrentUser
        {
            get
            {
                if ((User.Identity as UserIdentity) == null)
                    return null;

                return (User.Identity as UserIdentity).User;
            }
        }

        /// <summary>
        /// Gets a value indicating wether the user is logged in.
        /// </summary>
        protected bool IsAuthenticated { get { return CurrentUser != null; } }

        private readonly IDatabase _database;

        protected BaseController(IDatabase database)
        {
            _database = database;
            CacheDuration = TimeSpan.FromMinutes(5);
        }

        protected override IAsyncResult BeginExecute(RequestContext requestContext, AsyncCallback callback, object state)
        {
            var user = RequestAccessAuthorizer.Authorize(_database);
            if (user == null &&
                RequireAccount)
                requestContext.HttpContext.Response.Redirect("/login", true);

            ViewBag.IsAuthenticated = user != null;
            ViewBag.User = user;

            return base.BeginExecute(requestContext, callback, state);
        }

        protected ActionResult ErrorView(string message)
        {
            return View("ErrorView", (object)message);
        }

        #region Controller cache
        /// <summary>
        /// Gets the default name cached object in this controller.
        /// </summary>
        /// <param name="defaultObject">Default object if the cached object is not found or need update</param>
        protected object GetCachedObject(CachedDefaultObjectHandler defaultObject)
        {
            return GetCachedObject("_Default", CacheDuration, defaultObject);
        }

        /// <summary>
        /// Gets the default name cached object in this controller.
        /// </summary>
        /// <param name="cacheDuration">Amount of time the object will be cached</param>
        /// <param name="defaultObject">Default object if the cached object is not found or need update</param>
        protected object GetCachedObject(TimeSpan cacheDuration, CachedDefaultObjectHandler defaultObject)
        {
            return GetCachedObject("_Default", cacheDuration, defaultObject);
        }

        /// <summary>
        /// Gets a cached object in this controller.
        /// </summary>
        /// <param name="name">Name of object</param>
        /// <param name="defaultObject">Default object if the cached object is not found or need update</param>
        protected object GetCachedObject(string name, CachedDefaultObjectHandler defaultObject)
        {
            return GetCachedObject(name, CacheDuration, defaultObject);
        }

        /// <summary>
        /// Gets a cached object in this controller.
        /// </summary>
        /// <param name="name">Name of object</param>
        /// <param name="cacheDuration">Amount of time the object will be cached</param>
        /// <param name="defaultObject">Default object if the cached object is not found or need update</param>
        protected object GetCachedObject(string name, TimeSpan cacheDuration, CachedDefaultObjectHandler defaultObject)
        {
            return CacheManager.GetCachedObject(GetType().Name, name, cacheDuration, defaultObject);
        }

        /// <summary>
        /// Removes all cached objects in this controller.
        /// </summary>
        protected void ClearCache()
        {
            CacheManager.ClearCache(GetType().Name);
        }

        /// <summary>
        /// Removes a cached object in this controller.
        /// </summary>
        /// <param name="name">Name of object</param>
        protected bool RemoveCachedObject(string name)
        {
            return CacheManager.RemoveCachedObject(GetType().Name, name);
        }
        #endregion
    }
}
