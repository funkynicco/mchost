using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Minecraft
{
    public class InstanceInformation
    {
        public string Id { get; private set; }
        public InstanceStatus Status { get; private set; }
        public string PackageName { get; private set; }

        public InstanceConfiguration Configuration { get; private set; }

        public InstanceInformation(string id, InstanceStatus status, string packageName, InstanceConfiguration configuration)
        {
            Id = id;
            Status = status;
            PackageName = packageName;
            Configuration = configuration;
        }
    }
}
