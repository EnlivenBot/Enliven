using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common;
using Common.Music.Players;
using Discord.Commands;

namespace Bot.DiscordRelated.Commands.Modules;

public abstract class HavePlayerMusicModuleBase : MusicModuleBase
{
    protected new EnlivenLavalinkPlayer Player => base.Player!;

    protected override async Task<EnlivenLavalinkPlayer?> ResolvePlayerBeforeExecuteAsync(
        IReadOnlyCollection<Attribute> attributes, MusicCommandChannelInfo channelInfo, ulong? userVoiceChannelId)
    {
        var player = await base.ResolvePlayerBeforeExecuteAsync(attributes, channelInfo, userVoiceChannelId);
        if (player is null)
        {
            _ = await ReplyEntryAsync(NothingPlayingEntry, Constants.ShortTimeSpan);
            throw new CommandInterruptionException(NothingPlayingEntry);
        }

        var requireNonEmptyPlaylist = attributes
            .Any(attribute => attribute is RequireNonEmptyPlaylistAttribute);
        var requirePlayingTrack = attributes
            .Any(attribute => (attribute as RequireNonEmptyPlaylistAttribute)?.RequirePlayingTrack == true);
        await ReplyAndThrowIfAsync(requirePlayingTrack && player.CurrentTrack == null, NothingPlayingEntry);
        await ReplyAndThrowIfAsync(requireNonEmptyPlaylist && player.Playlist.IsEmpty, NothingPlayingEntry);

        return player;
    }

    public override async Task AfterExecuteAsync(CommandInfo command)
    {
        await this.RemoveMessageInvokerIfPossible();
        await base.AfterExecuteAsync(command);
    }
}