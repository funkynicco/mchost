using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework
{
    public interface ISettings
    {
        ServiceType ServiceType { get; }
        string ConnectionString { get; }
        string LogDirectory { get; }
        string MinecraftHostname { get; }
        string DefaultJavaFilename { get; }
    }

    public class Settings : ISettings
    {
        public ServiceType ServiceType { get; set; } = ServiceType.Web;
        public string ConnectionString { get; private set; }
        public string LogDirectory { get; private set; }
        public string MinecraftHostname { get; private set; }
        public string DefaultJavaFilename { get; private set; }

        public Settings()
        {
            ConnectionString = ConfigurationManager.AppSettings["ConnectionString"];
            LogDirectory = ConfigurationManager.AppSettings["LogDirectory"];
            MinecraftHostname = ConfigurationManager.AppSettings["MinecraftHostname"];
            DefaultJavaFilename = ConfigurationManager.AppSettings["DefaultJavaFilename"];
        }
    }
}
