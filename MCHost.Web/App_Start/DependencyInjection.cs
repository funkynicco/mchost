using MCHost.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;

namespace MCHost.Web
{
    public static class DependencyInjection
    {
        public static void Register(
            HttpConfiguration config,
            MyDependencyResolver dependencyResolver,
            IHostClient hostClient)
        {
            RegisterDependencies(dependencyResolver, hostClient);

            DependencyResolver.SetResolver(dependencyResolver);
            config.DependencyResolver = dependencyResolver;
        }

        public static void RegisterDependencies(IDependencyRegisterer config, IHostClient hostClient)
        {
            // the order matters of following dependency stack

            config.RegisterPersistent<ISettings, Settings>();
            config.RegisterPersistent<ILogger, Logger>();

            config.RegisterPersistent<IDatabase, Database>();

            config.RegisterPersistent<IWebSocketClientHandler, WebSocketClientHandler>();

            config.RegisterInstance(hostClient);
        }
    }
}