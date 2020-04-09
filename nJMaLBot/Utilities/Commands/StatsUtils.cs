using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Bot.Commands;
using Bot.Config;
using Bot.Config.Localization.Providers;
using Discord;
using NLog;
using Tyrrrz.Extensions;

namespace Bot.Utilities.Commands {
    public class StatsUtils {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        
        private static Temporary<int> _textChannelsCount =
            new Temporary<int>(() => Program.Client.Guilds.Sum(guild => guild.TextChannels.Count), TimeSpan.FromMinutes(5));

        private static Temporary<int> _voiceChannelsCount =
            new Temporary<int>(() => Program.Client.Guilds.Sum(guild => guild.VoiceChannels.Count), TimeSpan.FromMinutes(5));

        private static Temporary<int> _usersCount =
            new Temporary<int>(() => Program.Client.Guilds.Sum(guild => guild.MemberCount), TimeSpan.FromMinutes(5));

        private static Temporary<int> _commandUsagesCount =
            new Temporary<int>(() => GlobalDB.CommandStatistics.FindById("Global").UsagesList.Sum(pair => (int) pair.Value), TimeSpan.FromMinutes(5));

        private static Temporary<int> _commandUsersCount =
            new Temporary<int>(() => GlobalDB.CommandStatistics.Count(), TimeSpan.FromMinutes(5));

        [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse")]
        public static async Task<EmbedBuilder> PrintStats(IUser user, ILocalizationProvider loc) {
            var stats = GlobalDB.CommandStatistics.FindById(user?.Id.ToString() ?? "Global");
            var embedBuilder = new EmbedBuilder().WithColor(Color.Gold)
                                                 .WithTitle(loc.Get("Statistics.Title"))
                                                 .WithDescription(user == null
                                                      ? loc.Get("Statistics.GlobalStats")
                                                      : loc.Get("Statistics.UserStats").Format(user.Username));
            if (stats == null) {
                embedBuilder.WithColor(Color.Red)
                            .WithDescription(user == null ? loc.Get("Statistics.NoGlobalStats") : loc.Get("Statistics.NoUserStats").Format(user.Username));
                return embedBuilder;
            }

            IEnumerable<(string, double)> valueTuples = null;
            while (true) {
                try {
                    valueTuples = stats.UsagesList.GroupBy(pair => HelpUtils.CommandAliases.Value[pair.Key].First())
                                       .Where(pairs => !pairs.Key.IsHiddenCommand())
                                       .Select(pairs => (pairs.Key.Name.ToString(), pairs.Sum(pair => (double) pair.Value)))
                                       .OrderBy(tuple => tuple.Item1).ToList();
                    break;
                }
                catch (InvalidOperationException e) {
                    if (valueTuples != null) {
                        logger.Error(e, "Exception while printing stats");
                        throw;
                    }
                    // This exception appears that an element has appeared in ours that is not in the commands
                    stats.UsagesList = stats.UsagesList.Where(pair => HelpUtils.CommandAliases.Value.Contains(pair.Key))
                                            .ToDictionary(pair => pair.Key, pair => pair.Value);
                    GlobalDB.CommandStatistics.Upsert(stats);
                    // Assigning non null value to avoid endless cycle
                    valueTuples = new List<(string, double)>();
                }
            }

            embedBuilder.AddField(loc.Get("Statistics.ByCommands"),
                string.Join("\n", valueTuples.Select((tuple, i) => $"`{tuple.Item1}` - {tuple.Item2}")));

            if (user != null) return embedBuilder;
            var messageStats = GlobalDB.CommandStatistics.FindById("Messages");
            if (messageStats != null && messageStats.UsagesList.Count != 0) {
                embedBuilder.AddField(loc.Get("Statistics.ByMessages"),
                    messageStats.UsagesList.Select(pair => $"`{loc.Get("Statistics." + pair.Key)}` - {pair.Value}").JoinToString("\n"));
            }

            var musicTime = CommandHandler.GetTotalMusicTime();
            embedBuilder.AddField(loc.Get("Statistics.ByMusic"),
                loc.Get("Statistics.ByMusicFormatted").Format((int) musicTime.TotalDays, musicTime.Hours, musicTime.Minutes));

            embedBuilder.Fields.Insert(0, new EmbedFieldBuilder {
                Name = loc.Get("Statistics.ByGlobal"),
                Value = loc.Get("Statistics.ByGlobalFormatted0")
                           .Format(Program.Client.Guilds.Count, _textChannelsCount.Value, _voiceChannelsCount.Value, _usersCount.Value) +
                        loc.Get("Statistics.ByGlobalFormatted1").Format(_commandUsagesCount.Value, _commandUsersCount.Value)
            });
            return embedBuilder;
        }
    }
}