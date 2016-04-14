using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        /// <summary>
        /// Determines if the given text contains word or sentence that is ignored.
        /// </summary>
        /// <param name="text">Text to scan</param>
        bool ShouldIgnore(string text);
    }

    public class Configuration : IConfiguration
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
        private readonly object _lock = new object();

        private readonly List<string> _ignoreSentences = new List<string>();
        private readonly List<Regex> _ignoreRegex = new List<Regex>();

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

        public bool ShouldIgnore(string text)
        {
            foreach (var sentences in _ignoreSentences)
            {
                if (text.Contains(sentences))
                    return true;
            }

            foreach (var regex in _ignoreRegex)
            {
                if (regex.IsMatch(text))
                    return true;
            }

            return false;
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

            foreach (XmlNode node in doc.SelectSingleNode("Configuration/IgnoreMinecraftConsole").ChildNodes)
            {
                if (string.Compare(node.Name, "ignore", true) == 0)
                {
                    if (node.InnerText.Length > 0)
                        configuration._ignoreSentences.Add(node.InnerText);
                }
                else if (string.Compare(node.Name, "regex", true) == 0)
                {
                    if (node.InnerText.Length > 0)
                    {
                        var caseSensitive = true;

                        var caseAttribute = node.Attributes["Case"];
                        if (caseAttribute != null &&
                            string.Compare(caseAttribute.Value, "insensitive", true) == 0)
                            caseSensitive = false;

                        var regex = caseSensitive ? new Regex(node.InnerText) : new Regex(node.InnerText, RegexOptions.IgnoreCase);

                        configuration._ignoreRegex.Add(regex);
                    }
                }
                else if (node.NodeType != XmlNodeType.Comment)
                    throw new InvalidConfigurationException("Unknown node in Configuration/IgnoreMinecraftConsole: " + node.OuterXml);
            }

            return configuration;
        }

        public class InvalidConfigurationException : Exception
        {
            public InvalidConfigurationException(string message) :
                base(message)
            {
            }
        }
    }
}