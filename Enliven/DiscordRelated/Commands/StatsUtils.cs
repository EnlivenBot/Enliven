using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Bot.Utilities;
using Common;
using Common.Config;
using Common.Localization.Providers;
using Discord;
using Discord.Commands;
using NLog;
using Tyrrrz.Extensions;

namespace Bot.DiscordRelated.Commands {
    public class StatsUtils {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static Temporary<int> _textChannelsCount =
            new Temporary<int>(() => Program.Client.Guilds.Sum(guild => guild.TextChannels.Count), Constants.LongTimeSpan);

        private static Temporary<int> _voiceChannelsCount =
            new Temporary<int>(() => Program.Client.Guilds.Sum(guild => guild.VoiceChannels.Count), Constants.LongTimeSpan);

        private static Temporary<int> _usersCount =
            new Temporary<int>(() => Program.Client.Guilds.Sum(guild => guild.MemberCount), Constants.LongTimeSpan);

        private static Temporary<int> _commandUsagesCount =
            new Temporary<int>(() => StatisticsPart.Get("Global").UsagesList.Sum(pair => pair.Value), Constants.LongTimeSpan);

        private static Temporary<int> _commandUsersCount =
            new Temporary<int>(() => Database.CommandStatistics.Count(), Constants.LongTimeSpan);

        [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse")]
        public static EmbedBuilder BuildStats(IUser? user, ILocalizationProvider loc) {
            var stats = StatisticsPart.Get(user?.Id.ToString() ?? "Global");
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

            List<(CommandInfo Key, double)>? valueTuples = null;
            while (true) {
                try {
                    valueTuples = stats.UsagesList.GroupBy(pair => Program.Handler.CommandAliases[pair.Key].First())
                                       .Where(pairs => !pairs.Key.IsHiddenCommand())
                                       .Select(pairs => (pairs.Key, pairs.Sum(pair => (double) pair.Value)))
                                       .OrderBy(tuple => tuple.Item1.GetGroup()?.GroupName).ToList();
                    break;
                }
                catch (InvalidOperationException e) {
                    if (valueTuples != null) {
                        logger.Error(e, "Exception while printing stats");
                        throw;
                    }

                    // This exception appears that an element has appeared in ours that is not in the commands
                    stats.UsagesList = stats.UsagesList.Where(pair => Program.Handler.CommandAliases.Contains(pair.Key))
                                            .ToDictionary(pair => pair.Key, pair => pair.Value);
                    stats.Save();
                    // Assigning non null value to avoid endless cycle
                    valueTuples = new List<(CommandInfo, double)>();
                }
            }

            foreach (var grouping in valueTuples.GroupBy(tuple => tuple.Key.GetGroup()).OrderByDescending(tuples => tuples.Count())) {
                embedBuilder.AddField(grouping.Key!.GetLocalizedName(loc),
                    string.Join("\n", grouping.ToList().Select((tuple, i) => $"`{tuple.Item1.Name}` - {tuple.Item2}")), true);
            }

            if (user != null) return embedBuilder;
            var messageStats = StatisticsPart.Get("Messages");
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