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
using Common;
using Common.Music.Tracks;
using Discord;
using Discord.Commands;
using Enliven.MusicResolvers.Base;

namespace Bot.Commands.Music;

[SlashCommandAdapter]
[LongRunningCommand]
[Grouping("music")]
[RequireContext(ContextType.Guild)]
public sealed class PlayCommand : CreatePlayerMusicModuleBase {
    public MusicResolverService MusicResolverService { get; set; } = null!;

    [Command("play", RunMode = RunMode.Async)]
    [Alias("p")]
    [Summary("play0s")]
    public async Task Play([Remainder] [SlashCommandOptional] [Summary("play0_0s")] string? query = null) {
        await PlayInternal(query);
    }

    [Command("playnext", RunMode = RunMode.Async)]
    [Alias("pn")]
    [Summary("playnext0s")]
    public async Task PlayNext([Remainder] [SlashCommandOptional] [Summary("play0_0s")] string? query = null) {
        await PlayInternal(query, Player.Playlist.Count == 0 ? null : Player.CurrentTrackIndex + 1);
    }

    private async Task PlayInternal(string? query, int? position = null) {
        var messageInvoker = (Context as TextCommandsModuleContext)?.Message;
        var queries = await GetMusicQueries(messageInvoker, query.IsBlank(""));
        if (queries.Count != 0) {
            await Player.ResolveAndEnqueue(queries, new TrackRequester(Context.User), position);
        }

        await this.RemoveMessageInvokerIfPossible();
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
            else
                list.AddRange(message.Attachments.Select(attachment => attachment.Url));
        }

        return list;

        IEnumerable<string> ParseByLines(string query1) {
            return query1.Split('\n').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim());
        }
    }
}