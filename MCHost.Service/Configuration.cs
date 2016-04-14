using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace MCHost
{
    public interface IConfiguration
    {
        string this[string key] { get; }

        /// <summary>
        /// Gets a value in the Configuration/Values section.
        /// </summary>
        /// <param name="key">The key of the configuration value.</param>
        /// <exception cref="KeyNotFoundException"></exception>
        string GetValue(string key);
    }

    public class Configuration : IConfiguration
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
        private readonly object _lock = new object();

        private Configuration()
        {
        }

        public string this[string key]
        {
            get
            {
                return GetValue(key);
            }
        }

        public string GetValue(string key)
        {
            lock (_lock)
            {
                return _values[key.ToLower()];
            }
        }

        // static

        /// <summary>
        /// Loads the configuration.xml file.
        /// </summary>
        /// <exception cref="InvalidConfigurationException"></exception>
        public static IConfiguration Load(string filename)
        {
            var configuration = new Configuration();

            var doc = new XmlDocument();
            doc.Load(filename);

            configuration._values.Clear();

            foreach (XmlNode addNode in doc.SelectNodes("Configuration/Values/Add"))
            {
                var keyAttribute = addNode.Attributes["Key"];
                var valueAttribute = addNode.Attributes["Value"];

                if ((keyAttribute?.Value ?? "").Length == 0)
                    throw new InvalidConfigurationException("The 'Key' attribute value is missing in configuration: " + addNode.OuterXml);

                if ((valueAttribute?.Value ?? "").Length == 0)
                    throw new InvalidConfigurationException("The 'Value' attribute value is missing in configuration: " + addNode.OuterXml);

                var lowerKey = keyAttribute.Value.ToLower();

                if (configuration._values.ContainsKey(lowerKey))
                    throw new InvalidConfigurationException("Duplicate configuration value: " + keyAttribute.Value);

                configuration._values.Add(lowerKey, valueAttribute.Value);
            }

            return configuration;
        }
    }

    public class InvalidConfigurationException : Exception
    {
        public InvalidConfigurationException(string message) :
            base(message)
        {
        }
    }
}