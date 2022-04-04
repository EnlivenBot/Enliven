using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Autofac;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Common;
using Common.Music;
using Discord;
using Discord.Commands;

namespace Bot.Commands.Music {
    [Grouping("music")]
    [RequireContext(ContextType.Guild)]
    public sealed class PlayCommand : MusicModuleBase {
        [ShouldCreatePlayer]
        [Command("play", RunMode = RunMode.Async)]
        [Alias("p")]
        [Summary("play0s")]
        public async Task Play([Remainder] [Summary("play0_0s")] string? query = null) {
            if (!await IsPreconditionsValid)
                return;
            await PlayInternal(query);
        }

        [ShouldCreatePlayer]
        [Command("playnext", RunMode = RunMode.Async)]
        [Alias("pn")]
        [Summary("playnext0s")]
        public async Task PlayNext([Remainder] [Summary("play0_0s")] string? query = null) {
            if (!await IsPreconditionsValid)
                return;
            await PlayInternal(query, Player!.Playlist.Count == 0 ? -1 : Player.CurrentTrackIndex + 1);
        }

        private async Task PlayInternal(string? query, int position = -1) {
            var queries = await GetMusicQueries(Context.Message, query.IsBlank(""));
            if (queries.Count == 0) {
                Context.Message?.SafeDelete();
                if (MainDisplay != null) MainDisplay.NextResendForced = true;
                return;
            }

            MainDisplay?.ControlMessageResend();
            try {
                var resolvedQueries = await MusicController.ResolveQueries(queries);
                var username = Context.User?.Username ?? "Unknown";
                await Player!.TryEnqueue(resolvedQueries, username, position);
            }
            catch (TrackNotFoundException) {
                _ = ErrorMessageController
                    .AddEntry(Loc.Get("Music.NotFound", query!.SafeSubstring(100, "...")!))
                    .Update();
            }
        }
        
        private async Task<List<string>> GetMusicQueries(IMessage message, string query) {
            var list = new List<string>();
            list.AddRange(ParseByLines(query));
            if (message.Attachments.Count != 0 && message.Attachments.First().Filename == "message.txt") {
                var httpClient = ComponentContext.Resolve<HttpClient>();
                var messageTxtContext = await httpClient.GetStringAsync(message.Attachments.First().Url);
                list.AddRange(ParseByLines(messageTxtContext));
            }
            else {
                list.AddRange(message.Attachments.Select(attachment => attachment.Url));
            }

            return list;

            IEnumerable<string> ParseByLines(string query1) {
                return query1.Split('\n').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim());
            }
        }
    }
}