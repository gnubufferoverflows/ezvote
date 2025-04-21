using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using Discord;
using Discord.WebSocket;
using ezvote;

public class Program {
    private static DiscordSocketClient _client;
    public static readonly string EZPOLL_PREFIX_POLL_OPTION = "EZPOLL_OPTION_";
    public static readonly string EZPOLL_PREFIX_ABSTAIN = "EZPOLL_OPTIONABSTAIN";
    public static readonly string EZPOLL_PREFIX_FINALIZE = "EZPOLL_FINALIZE";
    public static readonly string EZPOLL_PREFIX_MODAL = "EZPOLL_MODAL_";
    public static readonly string EZPOLL_POLLID_PREFIX = "My poll id is: ";
    public static readonly int EZPOLL_OPTION_ABSTAIN_ID = -1;
    public static async Task Main()
    {
        _client = new DiscordSocketClient();
        _client.Log += Log;
        await _client.LoginAsync(TokenType.Bot, System.Environment.GetEnvironmentVariable("TOKEN"));
        await _client.StartAsync();
        _client.Ready += RunReady;
        _client.SlashCommandExecuted += SlashCommandHandler;
        _client.ButtonExecuted += ButtonExecuted;

        _client.ModalSubmitted += ModalSubmitted;
        

        await Task.Delay(-1);

    }


    private static async Task ModalSubmitted(SocketModal smc)
    {
        if(smc.Data.CustomId.StartsWith(EZPOLL_PREFIX_MODAL))
        {
            var tmp = smc.Data.CustomId.Split("_");
            string optionId = tmp[2];
            Guid pollId = Guid.Parse(tmp[3]);
            BotConfig.GetCachedConfig().PollData[pollId].Votes[smc.User.Id] = new Vote()
            {
                Explanation = smc.Data.Components.First(m => m.CustomId == "explanation").Value,
                OptionId = Int32.Parse(optionId)
            };
            BotConfig.SaveConfig(BotConfig.GetCachedConfig());
            await smc.RespondAsync(embed: QuickEmbeds.Success("Vote Cast"), ephemeral: true);
        }
    }

    private static async Task ButtonExecuted(SocketMessageComponent smc)
    {
        if(smc.Data.CustomId.StartsWith(EZPOLL_PREFIX_POLL_OPTION))
        {
            string strpollid = smc.Message.Embeds.First().Footer.Value.Text.Split(EZPOLL_POLLID_PREFIX)[1];
            Guid pollId = Guid.Parse(strpollid);
            if (BotConfig.GetCachedConfig().PollData[pollId].PollFinalized)
            {
                await smc.RespondAsync(embed: QuickEmbeds.Error("The poll is already finalized."), ephemeral: true);
                return;
            }
            string idSelected = smc.Data.CustomId.Split(EZPOLL_PREFIX_POLL_OPTION)[1];
            Modal m = new ModalBuilder()
                .WithTitle("Vote Explanation")
                .WithCustomId(EZPOLL_PREFIX_MODAL + idSelected + "_" + strpollid)
                .AddTextInput(new TextInputBuilder().WithLabel("Explanation").WithCustomId("explanation").WithMinLength(5).WithMaxLength(255).WithRequired(true).WithStyle(TextInputStyle.Paragraph))
                .Build();
            await smc.RespondWithModalAsync(m);
        }
        if(smc.Data.CustomId.StartsWith(EZPOLL_PREFIX_FINALIZE)) {
            Guid pollId = Guid.Parse(smc.Message.Embeds.First().Footer.Value.Text.Split(EZPOLL_POLLID_PREFIX)[1]);
            if (BotConfig.GetCachedConfig().PollData[pollId].PollOwner != smc.User.Id)
            {
                await smc.RespondAsync(embed: QuickEmbeds.PermissionError(), ephemeral: true);
                return;
            }
            if (BotConfig.GetCachedConfig().PollData[pollId].PollFinalized)
            {
                await smc.RespondAsync(embed: QuickEmbeds.Error("The poll is already finalized."), ephemeral: true);
                return;
            }
            Dictionary<int, int> freqTable = new Dictionary<int, int>();
            foreach(var (key, value) in BotConfig.GetCachedConfig().PollData[pollId].Votes)
            {
                if (!freqTable.ContainsKey(value.OptionId))
                {
                    freqTable[value.OptionId] = 0;
                }
                freqTable[value.OptionId]++;
            }
            var sort = freqTable.OrderByDescending(m => m.Value).ToDictionary(m => m.Key, m => m.Value);
            
            StringBuilder sb = new StringBuilder($"Results for poll '{smc.Message.Embeds.First().Title}':\n");
            sb.AppendLine();
            foreach (KeyValuePair<int, int> orderedKeys in sort)
            {
                if(orderedKeys.Key == -1)
                {
                    sb.AppendLine($"Abstentions: {orderedKeys.Value}");
                }
                else
                {
                    sb.AppendLine($"{BotConfig.GetCachedConfig().PollData[pollId].OptionsList[orderedKeys.Key]}: {orderedKeys.Value}");
                }
            }
            sort.Remove(-1);
            int total = 0;
            foreach(KeyValuePair<int, int> keyValuePair in sort)
            {
                total += keyValuePair.Value;
            }
            if(total == 0)
            {
                sb.Append("Nobody voted in this poll.");
            }
            else
            {
                int mostpopular = sort.First().Key;
                string mostpopularStr = BotConfig.GetCachedConfig().PollData[pollId].OptionsList[mostpopular];
                int threshold = BotConfig.GetCachedConfig().PollData[pollId].PassThreshold;
                double percent = ((double) sort.First().Value / (double) total) * 100D;
                sb.AppendLine($"The most popular option was {mostpopularStr} with {percent}% of the vote. It needed {threshold}% to pass.");
                sb.AppendLine();
                sb.AppendLine("To see the voter list, run `/listvoters`");
                sb.AppendLine("To see a voter's explanation, run `/explainvote`");
            }
            BotConfig.GetCachedConfig().PollData[pollId].PollFinalized = true;
            BotConfig.SaveConfig(BotConfig.GetCachedConfig());
            await smc.RespondAsync(embed: new EmbedBuilder().WithTitle("Poll Results").WithDescription(sb.ToString()).Build());
            
        }
        if (smc.Data.CustomId.StartsWith(EZPOLL_PREFIX_ABSTAIN))
        {
            Guid pollId = Guid.Parse(smc.Message.Embeds.First().Footer.Value.Text.Split(EZPOLL_POLLID_PREFIX)[1]);
            if (BotConfig.GetCachedConfig().PollData[pollId].PollFinalized)
            {
                await smc.RespondAsync(embed: QuickEmbeds.Error("The poll is already finalized."), ephemeral: true);
                return;
            }
            BotConfig.GetCachedConfig().PollData[pollId].Votes[smc.User.Id] = new Vote()
            {
                Explanation = "This user abstained from voting.",
                OptionId = -1
            };
            BotConfig.SaveConfig(BotConfig.GetCachedConfig());
            await smc.RespondAsync(embed: QuickEmbeds.Success("Vote Cast", "Your abstention has been recorded"), ephemeral: true);
        }
    }

    private static async Task SlashCommandHandler(SocketSlashCommand command)
    {
        if(command.CommandName == "explainvote")
        {
            var pollid = Guid.Parse((string)command.Data.Options.Where(option => option.Name == "pollid").FirstOrDefault().Value);
            var usr = command.Data.Options.Where(option => option.Name == "user").FirstOrDefault().Value as SocketGuildUser;
            if (BotConfig.GetCachedConfig().PollData.ContainsKey(pollid)) {
                if (BotConfig.GetCachedConfig().PollData[pollid].Votes.ContainsKey(usr.Id))
                {
                    if(BotConfig.GetCachedConfig().PollData[pollid].PollFinalized || command.User.Id == BotConfig.GetCachedConfig().PollData[pollid].PollOwner)
                    {
                        StringBuilder sb = new StringBuilder($"**Voter <@{usr.Id}>**\n");
                        if (BotConfig.GetCachedConfig().PollData[pollid].Votes[usr.Id].OptionId == -1)
                        {
                            sb.AppendLine("The user abstained from voting.");
                        }
                        else
                        {
                            sb.AppendLine("**Option selected: **" + BotConfig.GetCachedConfig().PollData[pollid].OptionsList[BotConfig.GetCachedConfig().PollData[pollid].Votes[usr.Id].OptionId]);
                            sb.AppendLine("Explanation: " + BotConfig.GetCachedConfig().PollData[pollid].Votes[usr.Id].Explanation);
                        }
                        await command.RespondAsync(sb.ToString(), ephemeral: true);
                    }
                    else
                    {
                        await command.RespondAsync(embed: QuickEmbeds.PermissionError(), ephemeral: true);
                        return;
                    }
                }
                else
                {
                    await command.RespondAsync(embed: QuickEmbeds.Error("This user didn't vote."), ephemeral: true);
                    return;
                }
            }
            else
            {
                await command.RespondAsync(embed: QuickEmbeds.Error("Poll could not be found."), ephemeral: true);
                return;
            }
        }
        if(command.CommandName == "listvoters")
        {
            try
            {
                var pollid = Guid.Parse((string) command.Data.Options.Where(option => option.Name == "pollid").FirstOrDefault().Value);
                if (BotConfig.GetCachedConfig().PollData.ContainsKey(pollid))
                {
                    if (BotConfig.GetCachedConfig().PollData[pollid].Votes.Count > 0)
                    {
                        StringBuilder sb = new StringBuilder("**Voters: **\n");
                        foreach(KeyValuePair<ulong, Vote> keys in BotConfig.GetCachedConfig().PollData[pollid].Votes)
                        {
                            sb.AppendLine($"<@{keys.Key}>");
                        }
                        if(sb.Length > 1000)
                        {
                            await command.RespondAsync(embed: QuickEmbeds.Error("The voter list is too large."), ephemeral: true);
                            return;
                        }
                        await command.RespondAsync(sb.ToString(), ephemeral: true);
                    }
                    else
                    {
                        await command.RespondAsync(embed: QuickEmbeds.Error("Nobody has voted yet."), ephemeral: true);
                        return;
                    }
                }
                else
                {
                    await command.RespondAsync(embed: QuickEmbeds.Error("Poll could not be found."), ephemeral: true);
                    return;
                }
            }
            catch(FormatException ex)
            {
                await command.RespondAsync(embed: QuickEmbeds.Error(ex.Message), ephemeral: true);
            }
            
        }
        if(command.CommandName == "newpoll")
        {
            var optionsRaw = (string) command.Data.Options.Where(option => option.Name == "options").FirstOrDefault().Value;
            var channel = (SocketChannel) command.Data.Options.Where(option => option.Name == "channel").FirstOrDefault().Value;
            var title = (string) command.Data.Options.Where(option => option.Name == "title").FirstOrDefault().Value;
            var description = (string) command.Data.Options.Where(option => option.Name == "description").FirstOrDefault().Value;
            var allowAbstain = (bool)command.Data.Options.Where(option => option.Name == "allowabstain").FirstOrDefault().Value;
            var threshold = Math.Ceiling((double) command.Data.Options.Where(option => option.Name == "threshold").FirstOrDefault().Value);

            if (!(channel is SocketTextChannel && channel is SocketGuildChannel))
            {
                await command.RespondAsync(embed: QuickEmbeds.Error("Invalid channel."), ephemeral: true);
                return;
            }

            if(command.GuildId == null)
            {
                await command.RespondAsync(embed: QuickEmbeds.Error("Sorry, but this command can only be sent in a server."), ephemeral: true);
                return;
            }

            var optionsReal = optionsRaw.Split(",");
            if(optionsRaw.Length < 2)
            {
                await command.RespondAsync(embed: QuickEmbeds.Error("Please specify at least two options."), ephemeral: true);
                return;
            }
            if(optionsReal.Length > 8)
            {
                await command.RespondAsync(embed: QuickEmbeds.Error("Too many options"), ephemeral: true);
                return;
            }
            if(optionsReal.Where(s => s.Length > 30 || s.Length < 1).ToImmutableArray().Length > 0)
            {
                await command.RespondAsync(embed: QuickEmbeds.Error("One of your poll options is too large. Please make it 30 characters or less."), ephemeral: true);
                return;
            }

            ezvote.Poll p = new ezvote.Poll();
            p.PollFinalized = false;
            p.PollOwner = command.User.Id;
            p.PassThreshold = (int) Math.Ceiling(threshold);
            p.Guild = (ulong) command.GuildId;
            p.Votes = new Dictionary<ulong, Vote>();
            p.OptionsList = new List<string>(optionsReal);
            var guid = Guid.NewGuid();
            var comps = new ComponentBuilder();
            for (int i = 0; i < optionsReal.Length; i++) {
                comps.WithButton(label: optionsReal[i], EZPOLL_PREFIX_POLL_OPTION + i);
            }
            if(allowAbstain)
            {
                comps.WithButton(label: "Abstain", emote: new Emoji("❌"), customId: EZPOLL_PREFIX_ABSTAIN);
            }
            comps.WithButton(label: "Finalize Poll", style: ButtonStyle.Danger, customId: EZPOLL_PREFIX_FINALIZE, emote: new Emoji("🗳"));
            var pembed = new EmbedBuilder()
                .WithColor(Color.Red)
                .WithTitle(title)
                .WithDescription(description + $"\n\nThis poll requires {threshold}% majority to pass.")
                .WithFooter($"{EZPOLL_POLLID_PREFIX}{guid.ToString()}")
                .Build();
            var textChan = channel as SocketTextChannel;
            BotConfig.GetCachedConfig().PollData.Add(guid, p);
            BotConfig.SaveConfig(BotConfig.GetCachedConfig());
            await textChan.SendMessageAsync(embed: pembed, components: comps.Build());
            await command.RespondAsync(embed: QuickEmbeds.Success("Poll Created", "Your poll has been created"), ephemeral: true);
                

            
        }
    }

    private static async Task RunReady()
    {
        try
        {
            File.ReadAllText("/app/data/config.yml");
        }
        catch(Exception ex)
        {
            BotConfig cfg = new BotConfig();
            cfg.CommandsCreated = false;
            cfg.PollData = new Dictionary<Guid, ezvote.Poll>();
            BotConfig.SaveConfig(cfg);
            BotConfig.LoadConfig();
        }
        
        if (!BotConfig.GetCachedConfig().CommandsCreated)
        {
            var listVotersCommand = new SlashCommandBuilder()
                .WithName("listvoters")
                .WithDescription("List the voters for a poll")
                .AddOption("pollid", ApplicationCommandOptionType.String, description: "Which poll?", isRequired: true)
                .Build();
            var explainVoteCommand = new SlashCommandBuilder()
                .WithName("explainvote")
                .WithDescription("Show a voter's explanations")
                .AddOption("pollid", ApplicationCommandOptionType.String, description: "Which poll?", isRequired: true)
                .AddOption("user", ApplicationCommandOptionType.User, description: "Who", isRequired: true)
                .Build();
            var createPollCommand = new SlashCommandBuilder();
            var commandPoll = createPollCommand.WithName("newpoll")
                .WithDescription("Create a new poll")
                .AddOption("title", ApplicationCommandOptionType.String, description: "Short description of poll", isRequired: true, maxLength: 250)
                .AddOption("description", ApplicationCommandOptionType.String, description: "Long description of poll", isRequired: true, minLength: 10, maxLength: 2000)
                .AddOption("options", ApplicationCommandOptionType.String, description: "Comma separated options list", isRequired: true, minLength: 5, maxLength: 300)
                .AddOption("allowabstain", ApplicationCommandOptionType.Boolean, description: "Would you like to automatically add an option to abstain?", isRequired: true)
                .AddOption("threshold", ApplicationCommandOptionType.Number, description: "Specify 1-100 the percentage that the highest performing option needs to pass.", minValue: 1, maxValue: 100, isRequired: true)
                .AddOption("channel", ApplicationCommandOptionType.Channel, description: "What channel to post this poll in?", isRequired: true).Build();

            await _client.CreateGlobalApplicationCommandAsync(commandPoll);
            await _client.CreateGlobalApplicationCommandAsync(listVotersCommand);
            await _client.CreateGlobalApplicationCommandAsync(explainVoteCommand);
            BotConfig.GetCachedConfig().CommandsCreated = true;
            BotConfig.SaveConfig(BotConfig.GetCachedConfig());
        }
    }

    private static async Task Log(LogMessage message)
    {
        Console.WriteLine(message.ToString());
    }
}
