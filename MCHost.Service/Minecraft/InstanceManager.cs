using MCHost.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Service.Minecraft
{
    public interface IInstance
    {
    }

    public class InstanceManager
    {
        class Instance : IInstance
        {
            // instance-id, thread(s), package, process info, resetable events etc...
        }

        public IInstance StartInstance(Package package)
        {
            throw new NotImplementedException();
        }
    }
}
