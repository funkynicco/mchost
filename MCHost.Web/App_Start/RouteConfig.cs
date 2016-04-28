using MCHost.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;

namespace MCHost.Web
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.Add(new WebSocketRouteHandler());

            routes.MapRoute(
                name: "Login",
                url: "login",
                defaults: new { controller = "Home", action = "Login" }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }

    internal class WebSocketRouteHandler : RouteBase, IRouteHandler
    {
        public IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            var resolver = (MyDependencyResolver)GlobalConfiguration.Configuration.DependencyResolver;
            return resolver.GetService<WebSocketHandler>();
        }

        ////////////////////////////////////////////////////////

        public override RouteData GetRouteData(HttpContextBase httpContext)
        {
            if (httpContext.Request.Url.AbsolutePath.ToLower() == "/service")
                return new RouteData(this, new WebSocketRouteHandler());

            return null;
        }

        public override VirtualPathData GetVirtualPath(RequestContext requestContext, RouteValueDictionary values)
        {
            return null;
        }
    }
}
