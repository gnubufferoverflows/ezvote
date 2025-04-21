using Discord;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ezvote
{
    public class Vote
    {
        public int OptionId { get; set; }
        public string Explanation { get; set; }
    }

    public class Poll
    {
        public bool PollFinalized { get; set; }

        public List<string> OptionsList { get; set; }
        public ulong Guild { get; set; }
        public int PassThreshold { get; set; }
        public Dictionary<ulong, Vote> Votes { get; set; }
        public ulong PollOwner { get; set; }
    }

    public class BotConfig
    {

        private static BotConfig? configCache;

        public Dictionary<Guid, Poll> PollData { get; set; }

        public bool CommandsCreated { get; set; }

        // so we don't have to load the config every time a value needs to be read from it
        public static BotConfig GetCachedConfig()
        {
            if (configCache == null)
            {
                LoadConfig();
            }
            return configCache;
        }



        //loads config, forcing the cache to update
        public static BotConfig LoadConfig()
        {
            string fileContents = File.ReadAllText("/app/data/config.yml");
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var p = deserializer.Deserialize<BotConfig>(fileContents);
            configCache = p;
            return p;
        }

        public static void SaveConfig(BotConfig cfg)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var yml = serializer.Serialize(cfg);
            File.WriteAllText("/app/data/config.yml", yml);
            LoadConfig(); // update the cache
        }

    }

}
