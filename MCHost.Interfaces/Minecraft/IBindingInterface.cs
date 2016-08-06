using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Interfaces.Minecraft
{
    public interface IBindingInterface
    {
        IPAddress Address { get; }
        string IP { get; }
        int Port { get; }
    }
}
