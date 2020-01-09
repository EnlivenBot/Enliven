using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bot.Config;
using Lavalink4NET;
using Lavalink4NET.Player;

namespace Bot.Music {
    public sealed class AdvancedPlayer : VoteLavalinkPlayer
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="LavalinkPlayer"/> class.
        /// </summary>
        /// <param name="lavalinkSocket">the lavalink socket</param>
        /// <param name="client">the discord client</param>
        /// <param name="guildId">the identifier of the guild that is controlled by the player</param>
        public AdvancedPlayer(LavalinkSocket lavalinkSocket, IDiscordClientWrapper client, ulong guildId, bool disconnectOnStop)
            : base(lavalinkSocket, client, guildId, disconnectOnStop)
        {
            // This constructor pattern must be always used for a custom player. You can not add nor
            // remove any constructor parameters. The player class will be instantiated using System.Activator.
        }

        /// <summary>
        ///     Updates the player volume asynchronously.
        /// </summary>
        /// <param name="volume">the player volume (0f - 10f)</param>
        /// <param name="normalize">
        ///     a value indicating whether if the <paramref name="volume"/> is out of range (0f -
        ///     10f) it should be normalized in its range. For example 11f will be mapped to 10f and
        ///     -20f to 0f.
        /// </param>
        /// <returns>a task that represents the asynchronous operation</returns>
        /// <exception cref="InvalidOperationException">
        ///     thrown if the player is not connected to a voice channel
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     thrown if the specified <paramref name="volume"/> is out of range (0f - 10f)
        /// </exception>
        /// <exception cref="InvalidOperationException">thrown if the player is destroyed</exception>
        public override async Task SetVolumeAsync(float volume = 1, bool normalize = false)
        {
            EnsureNotDestroyed();
            EnsureConnected();

            // call the base method, without using it the volume would remain the same.
            await base.SetVolumeAsync(volume, normalize);

            // store the volume of the player
            GuildConfig.Get(GuildId).SetVolume(volume).Save();;
        }

        /// <summary>
        ///     Asynchronously triggered when the player has connected to a voice channel.
        /// </summary>
        /// <param name="voiceServer">the voice server connected to</param>
        /// <param name="voiceState">the voice state</param>
        /// <returns>a task that represents the asynchronous operation</returns>
        public async override Task OnConnectedAsync(VoiceServer voiceServer, VoiceState voiceState) {
            await base.SetVolumeAsync(GuildConfig.Get(GuildId).Volume);
            await base.OnConnectedAsync(voiceServer, voiceState);
        }
    }
}