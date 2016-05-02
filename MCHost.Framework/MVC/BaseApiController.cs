using MCHost.Framework.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;

namespace MCHost.Framework.MVC
{
    public class BaseApiController : ApiController
    {
        /// <summary>
        /// Gets or sets the default cache duration for objects in this controller.
        /// </summary>
        protected TimeSpan CacheDuration { get; set; }

        private readonly IDatabase _database;
        private User _user;

        protected User CurrentUser { get { return _user; } }
        protected bool IsAuthenticated { get { return CurrentUser != null; } }

        public BaseApiController(IDatabase database)
        {
            _database = database;
            CacheDuration = TimeSpan.FromMinutes(5);
        }

        public override async Task<HttpResponseMessage> ExecuteAsync(HttpControllerContext controllerContext, CancellationToken cancellationToken)
        {
            _user = RequestAccessAuthorizer.Authorize(_database);

            return await base.ExecuteAsync(controllerContext, cancellationToken);
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
