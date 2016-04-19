using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MCHost.Framework.Minecraft
{
    public class InstanceConfiguration
    {
        [Required]
        [RegularExpression(@"^\d+\.\d+.\d+.\d+:\d+$")]
        public string BindInterface { get; set; }

        [Required]
        public string Motd { get; set; }

        public bool EnableCommandBlocks { get; set; }

        [Range(1, 32)]
        public int MaxPlayers { get; set; }
        public bool AnnouncePlayerAchievements { get; set; }

        [Required]
        public string JavaExecutable { get; set; }
        [Range(1, 32768)]
        public int JavaInitialMemoryMegabytes { get; set; }
        [Range(1, 32768)]
        public int JavaMaximumMemoryMegabytes { get; set; }
        [Required]
        public string MinecraftJarFilename { get; set; }

        public IDictionary<string, string> ExtraConfigurationValues { get; private set; }

        private InstanceConfiguration()
        {
        }

        /// <summary>
        /// Validates the configuration.
        /// </summary>
        /// <exception cref="ValidationException"></exception>
        public void Validate()
        {
            foreach (var member in GetType().GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where((a) => a.MemberType == MemberTypes.Field || a.MemberType == MemberTypes.Property))
            {
                object value = null;

                switch (member.MemberType)
                {
                    case MemberTypes.Field:
                        value = (member as FieldInfo).GetValue(this);
                        break;
                    case MemberTypes.Property:
                        value = (member as PropertyInfo).GetValue(this);
                        break;
                }

                Validator.ValidateValue(
                    value,
                    new ValidationContext(member),
                    member.GetCustomAttributes<ValidationAttribute>());
            }
        }

        public string Serialize()
        {
            var serializer = new DataSerializer('|', ':');

            serializer.Add(BindInterface);
            serializer.Add(Motd);
            serializer.Add(EnableCommandBlocks);
            serializer.Add(MaxPlayers);
            serializer.Add(AnnouncePlayerAchievements);

            serializer.Add(JavaExecutable);
            serializer.Add(JavaInitialMemoryMegabytes);
            serializer.Add(JavaMaximumMemoryMegabytes);
            serializer.Add(MinecraftJarFilename);

            var sb = new StringBuilder();

            foreach (var extraConfig in ExtraConfigurationValues)
            {
                if (sb.Length > 0)
                    sb.Append(',');

                sb.Append(extraConfig.Key.Replace("=", "<[#EQ]>").Replace(",", "<[#CO]>"));
                sb.Append('=');
                sb.Append(extraConfig.Value.Replace("=", "<[#EQ]>").Replace(",", "<[#CO]>"));
            }

            serializer.Add(sb.ToString());

            return serializer.ToString();
        }

        // static

        public static InstanceConfiguration Deserialize(string data)
        {
            var deserializer = new DataDeserializer(data);

            var config = Default;

            config.BindInterface = deserializer.GetString();
            config.Motd = deserializer.GetString();
            config.EnableCommandBlocks = deserializer.GetBoolean();
            config.MaxPlayers = deserializer.GetInt32();
            config.AnnouncePlayerAchievements = deserializer.GetBoolean();

            config.JavaExecutable = deserializer.GetString();
            config.JavaInitialMemoryMegabytes = deserializer.GetInt32();
            config.JavaMaximumMemoryMegabytes = deserializer.GetInt32();
            config.MinecraftJarFilename = deserializer.GetString();

            var extraConfig = deserializer.GetString().Split(',');
            if (extraConfig.Length > 0)
            {
                foreach (var ex in extraConfig)
                {
                    var exd = ex.Split('=');
                    if (exd.Length == 2)
                    {
                        var key = exd[0].Replace("<[#EQ]>", "=").Replace("<[#CO]>", ",");
                        var value = exd[1].Replace("<[#EQ]>", "=").Replace("<[#CO]>", ",");

                        config.ExtraConfigurationValues[key] = value;
                    }
                }
            }

            return config;
        }

        public static InstanceConfiguration Default
        {
            get
            {
                return new InstanceConfiguration()
                {
                    BindInterface = "0.0.0.0:25565",
                    Motd = "Minecraft",
                    EnableCommandBlocks = true,
                    MaxPlayers = 20,
                    AnnouncePlayerAchievements = true,

                    JavaExecutable = "java",
                    JavaInitialMemoryMegabytes = 256,
                    JavaMaximumMemoryMegabytes = 1024,
                    MinecraftJarFilename = "server.jar",

                    ExtraConfigurationValues = new Dictionary<string, string>()
                };
            }
        }
    }
}
