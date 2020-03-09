using System;
using System.Threading.Tasks;
using Bot.Config;
using Lavalink4NET;
using Lavalink4NET.Player;

namespace Bot.Music.Players {
    public class AdvancedLavalinkPlayer : LavalinkPlayer {
        public AdvancedLavalinkPlayer(LavalinkSocket lavalinkSocket, IDiscordClientWrapper client, ulong guildId, bool disconnectOnStop) : base(lavalinkSocket,
            client, guildId, disconnectOnStop) { }

        public override async Task OnConnectedAsync(VoiceServer voiceServer, VoiceState voiceState) {
            await base.SetVolumeAsync(GuildConfig.Get(GuildId).Volume);
            await base.OnConnectedAsync(voiceServer, voiceState);
        }

        public override async Task SetVolumeAsync(float volume = 1, bool normalize = false) {
            volume = Math.Min(Math.Max(volume, 0), 1.5f);
            await base.SetVolumeAsync(volume, false);
            GuildConfig.Get(GuildId).SetVolume(volume).Save();
        }
    }
}