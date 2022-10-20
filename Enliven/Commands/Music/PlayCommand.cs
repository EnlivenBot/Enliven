using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Autofac;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Attributes;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Commands.Modules.Contexts;
using Bot.DiscordRelated.Interactions;
using Bot.DiscordRelated.Music;
using Common;
using Common.Localization.Entries;
using Common.Music;
using Discord;
using Discord.Commands;

namespace Bot.Commands.Music {
    [SlashCommandAdapter]
    [LongRunningCommand]
    [Grouping("music")]
    [RequireContext(ContextType.Guild)]
    public sealed class PlayCommand : MusicModuleBase {
        [ShouldCreatePlayer]
        [Command("play", RunMode = RunMode.Async)]
        [Alias("p")]
        [Summary("play0s")]
        public async Task Play([Remainder] [SlashCommandOptional(false)] [Summary("play0_0s")] string? query = null) {
            await PlayInternal(query);
        }

        [ShouldCreatePlayer]
        [Command("playnext", RunMode = RunMode.Async)]
        [Alias("pn")]
        [Summary("playnext0s")]
        public async Task PlayNext([Remainder] [SlashCommandOptional(false)] [Summary("play0_0s")] string? query = null) {
            await PlayInternal(query, Player.Playlist.Count == 0 ? -1 : Player.CurrentTrackIndex + 1);
        }

        private async Task PlayInternal(string? query, int position = -1) {
            var messageInvoker = (Context as TextCommandsModuleContext)?.Message;
            var queries = await GetMusicQueries(messageInvoker, query.IsBlank(""));
            var mainPlayerDisplay = await GetMainPlayerDisplay();
            if (queries.Count != 0) {
                await StartResolvingInternal(query, position, mainPlayerDisplay, queries);
                return;
            }

            if (Context.NeedResponse)
                await mainPlayerDisplay.ResendControlMessageWithOverride(OverrideSendingControlMessage);
            else {
                await this.RemoveMessageInvokerIfPossible();
                mainPlayerDisplay.NextResendForced = true;
                await mainPlayerDisplay.ControlMessageResend();
            }
        }

        private async Task StartResolvingInternal(string? query, int position, EmbedPlayerDisplay mainPlayerDisplay, IEnumerable<string> queries) {
            if (Context.NeedResponse) await mainPlayerDisplay.ResendControlMessageWithOverride(OverrideSendingControlMessage, false);
            else _ = mainPlayerDisplay.ControlMessageResend();
            try {
                var resolvedQueries = await MusicController.ResolveQueries(queries);
                var username = Context.User?.Username ?? "Unknown";
                await Player.TryEnqueue(resolvedQueries, username, position);
            }
            catch (TrackNotFoundException) {
                await this.ReplyFailFormattedAsync(new EntryLocalized("Music.NotFound", query!.SafeSubstring(100, "...")!), true);
            }
        }

        private async Task<List<string>> GetMusicQueries(IMessage? message, string query) {
            var list = new List<string>();
            list.AddRange(ParseByLines(query));
            if (message != null) {
                if (message.Attachments.Count != 0 && message.Attachments.First().Filename == "message.txt") {
                    var httpClient = ComponentContext.Resolve<HttpClient>();
                    var messageTxtContext = await httpClient.GetStringAsync(message.Attachments.First().Url);
                    list.AddRange(ParseByLines(messageTxtContext));
                }
                else {
                    list.AddRange(message.Attachments.Select(attachment => attachment.Url));
                }
            }

            return list;

            IEnumerable<string> ParseByLines(string query1) {
                return query1.Split('\n').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim());
            }
        }
    }
}