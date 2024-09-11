using System.Threading.Tasks;
using Bot.Commands.Chains;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions;
using Bot.DiscordRelated.MessageComponents;
using Bot.Utilities.Collector;
using Discord.Commands;
using Lavalink4NET.Rest.Entities.Tracks;

namespace Bot.Commands;

[SlashCommandAdapter]
[Grouping("music")]
[RequireContext(ContextType.Guild)]
public sealed class AdvancedSearchCommands : MusicModuleBase
{
    public MessageComponentService ComponentService { get; set; } = null!;
    public CollectorService CollectorService { get; set; } = null!;
    
    [Command("youtube", RunMode = RunMode.Async)]
    [Alias("y", "yt")]
    [Summary("youtube0s")]
    public async Task SearchYoutube([Summary("play0_0s")] [Remainder] string query)
    {
        new AdvancedMusicSearchChain(GuildConfig, Player!, Context.Channel, Context.User, TrackSearchMode.YouTube,
            query, AudioService, ComponentService, CollectorService).Start();
    }

    [Command("soundcloud", RunMode = RunMode.Async)]
    [Alias("sc")]
    [Summary("soundcloud0s")]
    public async Task SearchSoundCloud([Summary("play0_0s")] [Remainder] string query)
    {
        new AdvancedMusicSearchChain(GuildConfig, Player!, Context.Channel, Context.User, TrackSearchMode.SoundCloud,
            query, AudioService, ComponentService, CollectorService).Start();
    }
}