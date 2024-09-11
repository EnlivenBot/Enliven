using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common.Music.Players;
using Lavalink4NET.Players;

namespace Bot.DiscordRelated.Commands.Modules;

public abstract class CreatePlayerMusicModuleBase : MusicModuleBase
{
    protected new EnlivenLavalinkPlayer Player => base.Player!;

    protected override async Task<EnlivenLavalinkPlayer?> ResolvePlayerBeforeExecuteAsync(
        IReadOnlyCollection<Attribute> attributes, MusicCommandChannelInfo channelInfo, ulong? userVoiceChannelId)
    {
        var player = await base.ResolvePlayerBeforeExecuteAsync(attributes, channelInfo, userVoiceChannelId);
        if (player is not null)
            return player;
        
        var playerRetrieveOptions = new PlayerRetrieveOptions
        {
            ChannelBehavior = PlayerChannelBehavior.Join
        };

        return await CheckUserAndCreatePlayerAsync(playerRetrieveOptions);
    }
}