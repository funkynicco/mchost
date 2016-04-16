using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Service.Minecraft
{
    public class MinecraftConfiguration
    {
        private readonly string _filename;
        private readonly Dictionary<string, string> _properties = new Dictionary<string, string>();

        private MinecraftConfiguration(string filename)
        {
            _filename = filename;
        }

        public bool TryGetValue(string key, out string value)
        {
            return _properties.TryGetValue(key, out value);
        }

        public void SetValue(string key, string value)
        {
            _properties[key] = value;
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("#Minecraft server properties");
            sb.AppendLine("#" + DateTime.UtcNow.ToLongDateString() + " " + DateTime.UtcNow.ToLongTimeString());

            foreach (var property in _properties)
            {
                sb.AppendLine($"{property.Key}={property.Value}");
            }

            File.WriteAllText(_filename, sb.ToString());
        }

        // static

        public static MinecraftConfiguration FromFile(string filename)
        {
            var config = new MinecraftConfiguration(filename);

            var lines = File.ReadAllLines(filename);
            for (int i = 0; i < lines.Length; ++i)
            {
                lines[i] = lines[i].Trim();

                if (!lines[i].StartsWith("#") &&
                    lines[i].Length > 0)
                {
                    var pos = lines[i].IndexOf('=');
                    if (pos != -1)
                    {
                        var key = lines[i].Substring(0, pos).Trim();
                        var value = lines[i].Substring(pos + 1).Trim();

                        if (key.Length > 0)
                            config._properties.Add(key, value);
                    }
                }
            }

            //config._properties
            return config;
        }
    }
}
