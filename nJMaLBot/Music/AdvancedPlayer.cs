using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bot.Config;
using Discord;
using Lavalink4NET;
using Lavalink4NET.Player;

namespace Bot.Music {
    public sealed class AdvancedPlayer : VoteLavalinkPlayer
    {
        public AdvancedPlayer(LavalinkSocket lavalinkSocket, IDiscordClientWrapper client, ulong guildId, bool disconnectOnStop)
            : base(lavalinkSocket, client, guildId, disconnectOnStop) { }
        
        public override async Task SetVolumeAsync(float volume = 1, bool normalize = false)
        {
            EnsureNotDestroyed();
            EnsureConnected();

            await base.SetVolumeAsync(volume, normalize);

            GuildConfig.Get(GuildId).SetVolume(volume).Save();;
        }
        
        public async override Task OnConnectedAsync(VoiceServer voiceServer, VoiceState voiceState) {
            await base.SetVolumeAsync(GuildConfig.Get(GuildId).Volume);
            await base.OnConnectedAsync(voiceServer, voiceState);
        }

        private IMessage ControlMessage;

        public void SetControlMessage(IMessage message) {
            
        }
    }
}