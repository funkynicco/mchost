using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;
using System.Web.Http;
using System.IO;
using System.Configuration;
using MCHost.Framework;
using System.Globalization;
using System.Threading;
using System.Text.RegularExpressions;

namespace MCHost.Web
{
    public class Global : HttpApplication
    {
        private static readonly HostClient _hostClient = new HostClient();

        public static MyDependencyResolver DependencyResolver { get; private set; } = new MyDependencyResolver();

        void Application_Start(object sender, EventArgs e)
        {
            // Code that runs on application startup
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure((config) =>
                {
                    DependencyInjection.Register(config, DependencyResolver, _hostClient);
                    WebApiConfig.Register(config);
                });
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            var hostAddressMatch = Regex.Match(
                ConfigurationManager.AppSettings["MCHostService"],
                @"^([^:\s]+):(\d+)$",
                RegexOptions.IgnoreCase);
            if (!hostAddressMatch.Success)
                throw new Exception("The MCHostService parameter is invalid in Web.config");

            _hostClient.SetWebSocketClientHandler(DependencyResolver.GetDependency<IWebSocketClientHandler>());

            _hostClient.Connect(hostAddressMatch.Groups[1].Value, int.Parse(hostAddressMatch.Groups[2].Value));
        }

        void Application_End()
        {
            _hostClient.Dispose();
            AsyncTask.Shutdown();
            DependencyResolver.GlobalDispose();
        }

        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            var cultureInfo = new CultureInfo("en-us");

            cultureInfo.DateTimeFormat.LongDatePattern = "yyyy-MM-dd";
            cultureInfo.DateTimeFormat.LongTimePattern = "HH:mm";
            cultureInfo.DateTimeFormat.FullDateTimePattern = "yyyy-MM-dd HH:mm:ss";
            cultureInfo.DateTimeFormat.ShortDatePattern = "yyyy-MM-dd";
            cultureInfo.DateTimeFormat.ShortTimePattern = "HH:mm";

            Thread.CurrentThread.CurrentUICulture = cultureInfo;
            Thread.CurrentThread.CurrentCulture = cultureInfo;
        }
    }
}