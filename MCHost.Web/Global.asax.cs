using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;
using System.Web.Http;

namespace MCHost.Web
{
    public class Global : HttpApplication
    {
        public static HostClient HostClient { get; private set; }

        void Application_Start(object sender, EventArgs e)
        {
            // Code that runs on application startup
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            HostClient = new HostClient();
            HostClient.Connect("127.0.0.1", 25920);
        }
        
        void Application_End()
        {
            HostClient.Dispose();
            HostClient = null;
        }
    }
}