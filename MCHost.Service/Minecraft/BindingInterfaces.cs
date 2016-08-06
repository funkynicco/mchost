using MCHost.Interfaces.Minecraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace MCHost.Service.Minecraft
{
    public static class BindingInterfaces
    {
        class BindingInterface : IBindingInterface
        {
            private readonly string _ipString;

            public IPAddress Address { get; private set; }
            public string IP { get { return _ipString; } }
            public int Port { get; private set; }

            public BindingInterface(string ipString, int port)
            {
                _ipString = ipString;

                Address = IPAddress.Parse(ipString);
                Port = port;
            }
        }

        private static readonly Stack<IBindingInterface> _interfaces = new Stack<IBindingInterface>();
        private static int _allocatedInterfaces = 0;
        private static readonly object _lock = new object();

        public static int Count
        {
            get
            {
                lock (_lock)
                {
                    return _interfaces.Count;
                }
            }
        }

        public static IBindingInterface AllocateBindingInterface()
        {
            lock (_lock)
            {
                if (_interfaces.Count == 0)
                    throw new InvalidOperationException("There are no more interfaces.");

                ++_allocatedInterfaces;
                return _interfaces.Pop();
            }
        }

        public static void FreeBindingInterface(IBindingInterface bindingInterface)
        {
            lock (_lock)
            {
                if (_allocatedInterfaces == 0)
                    throw new InvalidOperationException("Possible attempts to free same interface more than once.");

                --_allocatedInterfaces;
                _interfaces.Push(bindingInterface);
            }
        }

        public static int Load(XmlNode node)
        {
            lock (_lock)
            {
                if (_allocatedInterfaces > 0)
                    throw new InvalidOperationException("Cannot load BindingInterfaces when there are allocated interfaces.");

                _interfaces.Clear();

                var hashCheck = new HashSet<string>();
                var interfaces = new List<BindingInterface>();

                foreach (XmlNode bindingNode in node.SelectNodes("Binding"))
                {
                    Match match;
                    var valueAttribute = bindingNode.Attributes["Value"];
                    if (valueAttribute == null ||
                        !(match = Regex.Match(valueAttribute.Value, @"^(\d+\.\d+.\d+.\d+):(\d+)$")).Success)
                        throw new InvalidConfigurationException("Value attribute was not found or contains invalid data: " + valueAttribute.OuterXml);

                    if (hashCheck.Contains(valueAttribute.Value))
                        throw new InvalidConfigurationException("Duplicated interface bindings: " + valueAttribute.Value);

                    hashCheck.Add(valueAttribute.Value);

                    interfaces.Add(new BindingInterface(
                        match.Groups[1].Value, // ip
                        int.Parse(match.Groups[2].Value))); // port
                }

                // add the interfaces to the stack in inverted order so that the first binding in the xml config gets used first
                for (int i = interfaces.Count - 1; i >= 0; --i)
                {
                    _interfaces.Push(interfaces[i]);
                }

                return _interfaces.Count;
            }
        }
    }
}
