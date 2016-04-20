using MCHost.Framework.Network;
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

        public void Serialize(IDataWriter stream)
        {
            stream.Write(BindInterface);
            stream.Write(Motd);
            stream.Write(EnableCommandBlocks);
            stream.Write(MaxPlayers);
            stream.Write(AnnouncePlayerAchievements);

            stream.Write(JavaExecutable);
            stream.Write(JavaInitialMemoryMegabytes);
            stream.Write(JavaMaximumMemoryMegabytes);
            stream.Write(MinecraftJarFilename);

            stream.Write(ExtraConfigurationValues.Count);
            foreach (var extraConfig in ExtraConfigurationValues)
            {
                stream.Write(extraConfig.Key);
                stream.Write(extraConfig.Value);
            }
        }

        // static

        public static InstanceConfiguration Deserialize(IDataReader stream)
        {
            var config = Default;

            config.BindInterface = stream.ReadString();
            config.Motd = stream.ReadString();
            config.EnableCommandBlocks = stream.ReadBoolean();
            config.MaxPlayers = stream.ReadInt32();
            config.AnnouncePlayerAchievements = stream.ReadBoolean();

            config.JavaExecutable = stream.ReadString();
            config.JavaInitialMemoryMegabytes = stream.ReadInt32();
            config.JavaMaximumMemoryMegabytes = stream.ReadInt32();
            config.MinecraftJarFilename = stream.ReadString();

            var numberOfExtraConfig = stream.ReadInt32();
            while (numberOfExtraConfig-- > 0)
            {
                var key = stream.ReadString();
                var value = stream.ReadString();

                config.ExtraConfigurationValues[key] = value;
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
