using System.Threading.Tasks;
using Bot.DiscordRelated.Commands;
using Bot.DiscordRelated.Commands.Modules;
using Bot.DiscordRelated.Interactions;
using Common.Localization.Entries;
using Discord.Commands;
using Lavalink4NET.Players;

namespace Bot.Commands;

[SlashCommandAdapter]
[Grouping("music")]
[RequireContext(ContextType.Guild)]
public sealed class ResumeCommand : MusicModuleBase {
    [Command("resume", RunMode = RunMode.Async)]
    [Alias("unpause")]
    [Summary("resume0s")]
    public async Task Resume() {
        if (Player is null) {
            if (AudioService.TryGetPlayerLaunchOptionsFromLastRun(Context.Guild.Id, out var createOptions)) {
                var playerRetrieveOptions = new PlayerRetrieveOptions()
                    { ChannelBehavior = PlayerChannelBehavior.Join };
                var player = await CheckUserAndCreatePlayerAsync(playerRetrieveOptions, createOptions);
                player.WriteToQueueHistory(new EntryLocalized("PlayerHistory.PlayerRestored", Context.User.Mention));
                return;
            }

            await this.ReplyFailFormattedAsync(new EntryLocalized("Music.NoSnapshotFoundToResume"), true);
            return;
        }

        if (Player.State != PlayerState.Paused) return;

        await Player.ResumeAsync();
        Player.WriteToQueueHistory(new EntryLocalized("PlayerHistory.Resume", Context.User.Mention));
    }
}