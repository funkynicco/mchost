using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Minecraft
{
    public enum InstanceStatus
    {
        Idle,
        Starting,
        Running,
        Stopping,
        Stopped,
        Error
    }
}
