using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Bot.Commands;
using Bot.Config;
using Bot.Config.Localization.Providers;
using Discord;
using Tyrrrz.Extensions;

namespace Bot.Utilities.Commands {
    public class StatsUtils {
        private static Temporary<int> _textChannelsCount =
            new Temporary<int>(() => Program.Client.Guilds.Sum(guild => guild.TextChannels.Count), TimeSpan.FromMinutes(5));

        private static Temporary<int> _voiceChannelsCount =
            new Temporary<int>(() => Program.Client.Guilds.Sum(guild => guild.VoiceChannels.Count), TimeSpan.FromMinutes(5));

        private static Temporary<int> _usersCount =
            new Temporary<int>(() => Program.Client.Guilds.Sum(guild => guild.Users.Count), TimeSpan.FromMinutes(5));

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

            var valueTuples = stats.UsagesList.GroupBy(pair => HelpUtils.CommandAliases.Value[pair.Key].First())
                                   .Where(pairs => !pairs.Key.IsHiddenCommand())
                                   .Select(pairs => (pairs.Key.Name.ToString(), pairs.Sum(pair => (double) pair.Value)));
            embedBuilder.AddField(loc.Get("Statistics.ByCommands"),
                string.Join("\n", valueTuples.Select((tuple, i) => $"`{tuple.Item1}` - {tuple.Item2}")));

            if (user != null) return embedBuilder;
            var messageStats = GlobalDB.CommandStatistics.FindById("Messages");
            if (messageStats != null) {
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