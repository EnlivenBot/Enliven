using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization;
using Bot.Utilities;
using Bot.Utilities.Commands;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Tyrrrz.Extensions;

namespace Bot.Commands {
    [Grouping("utils")]
    public class LogCommand : AdvancedModuleBase {
        [Command("history", RunMode = RunMode.Async)]
        [Summary("history0s")]
        public async Task PrintChanges(
            [Remainder] [Summary("history0_0s")] string id) {
            var channelId = Context.Channel.Id;
            var messageId = id;
            if (id.Contains('-')) {
                channelId = Convert.ToUInt64(id.Split('-')[0]);
                messageId = id.Split('-')[1];
            }

            await MessageHistoryManager.PrintLog(Convert.ToUInt64(messageId), channelId, (SocketTextChannel) Context.Channel, (IGuildUser) Context.User);
        }

        [Command("stats", RunMode = RunMode.Async)]
        [Summary("stats0s")]
        public async Task Stats() {
            await PrintStats(null);
        }

        [Command("userstats", RunMode = RunMode.Async)]
        [Summary("userstats0s")]
        public async Task UserStats([Summary("userstats0_0s")] IUser user) {
            await PrintStats(user);
        }

        [Command("userstats", RunMode = RunMode.Async)]
        [Summary("userstats1s")]
        public async Task UserStats() {
            await PrintStats(Context.User);
        }

        [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse")]
        private async Task PrintStats(IUser user) {
            var stats = GlobalDB.CommandStatistics.FindById(user?.Id.ToString() ?? "Global");
            var embedBuilder = new EmbedBuilder().WithColor(Color.Gold)
                                                 .WithTitle(Loc.Get("Statistics.Title"))
                                                 .WithDescription(user == null
                                                      ? Loc.Get("Statistics.GlobalStats")
                                                      : Loc.Get("Statistics.UserStats").Format(user.Username));
            if (stats == null) {
                embedBuilder.WithColor(Color.Red)
                            .WithDescription(user == null ? Loc.Get("Statistics.NoGlobalStats") : Loc.Get("Statistics.NoUserStats").Format(user.Username));
                await ReplyAsync(null, false, embedBuilder.Build());
                return;
            }

            var valueTuples = stats.UsagesList.GroupBy(pair => HelpUtils.CommandAliases.Value[pair.Key].First())
                                   .Where(pairs => !pairs.Key.IsHiddenCommand())
                                   .Select(pairs => (pairs.Key.Name.ToString(), pairs.Sum(pair => pair.Value)));
            embedBuilder.AddField(Loc.Get("Statistics.ByCommands"),
                string.Join("\n", valueTuples.Select((tuple, i) => $"`{tuple.Item1}` - {tuple.Item2}")));

            if (user == null) {
                var messageStats = GlobalDB.CommandStatistics.FindById("Messages");
                if (messageStats != null) {
                    embedBuilder.AddField(Loc.Get("Statistics.ByMessages"),
                        messageStats.UsagesList.Select(pair => $"`{Loc.Get("Statistics." + pair.Key)}` - {pair.Value}").JoinToString("\n"));
                }
            }
            await ReplyAsync(null, false, embedBuilder.Build());
        }
    }
}