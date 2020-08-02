using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization.Entries;
using Bot.Config.Localization.Providers;
using Bot.Music;
using Bot.Utilities;
using Discord;
using Lavalink4NET;
using Lavalink4NET.Player;
using NLog;

namespace Bot.DiscordRelated.Music {
    public class AdvancedLavalinkPlayer : LavalinkPlayer {
        // ReSharper disable once InconsistentNaming
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        protected GuildConfig GuildConfig;
        protected IGuild Guild;
        public readonly ILocalizationProvider Loc;
        protected BassBoostMode BassBoostMode { get; private set; } = BassBoostMode.Off;
        private int _updateFailCount;
        private int UpdateFailThreshold = 2;
        protected bool IsExternalEmojiAllowed { get; set; } = true;

        public AdvancedLavalinkPlayer(ulong guildId) {
            Guild = Program.Client.GetGuild(guildId);
            GuildConfig = GuildConfig.Get(guildId);
            Loc = new GuildLocalizationProvider(GuildConfig);
        }

        public override async Task OnConnectedAsync(VoiceServer voiceServer, VoiceState voiceState) {
            await SetVolumeAsync(GuildConfig.Get(GuildId).Volume);
            await base.OnConnectedAsync(voiceServer, voiceState);
        }

        public virtual async Task SetVolumeAsync(int volume = 100) {
            volume = volume.Normalize(0, 150);
            await base.SetVolumeAsync((float)volume / 100);
            var guildConfig = GuildConfig.Get(GuildId);
            guildConfig.Volume = volume;
            guildConfig.Save();
        }

        [Obsolete]
        public override async Task SetVolumeAsync(float volume = 1, bool normalize = false) {
            await SetVolumeAsync((int)(volume * 100));
        }

        public virtual void SetBassBoostMode(BassBoostMode mode) {
            BassBoostMode = mode;
        }

        public bool IsShutdowned { get; private set; }

        public virtual void Shutdown(EntryLocalized reason, bool needSave = true) {
            Shutdown(reason.Get(Loc), needSave);
        }

        public virtual Task Shutdown(string reason, bool needSave = true) {
            IsShutdowned = true;
            base.Dispose();
            return Task.CompletedTask;
        }

        public virtual Task Shutdown(bool needSave = true) {
            Shutdown(Loc.Get("Music.PlaybackStopped"), needSave);
            return Task.CompletedTask;
        }

        /// <summary>
        /// This method is called only from third-party code.
        /// </summary>
        [Obsolete]
        public override void Dispose() {
            if (!IsShutdowned) logger.Error("Player disposed. Stacktrace: \n{stacktrace}", new StackTrace().ToString());
            Shutdown();
        }

        /// <summary>
        /// Try to update player's data (integration, embed, etc)
        /// </summary>
        /// <exception cref="ObjectDisposedException">Throw when player has been disposed die wrong state</exception>
        public virtual void UpdatePlayer() {
            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (State) {
                case PlayerState.Destroyed:
                case PlayerState.NotConnected:
                    _updateFailCount++;
                    break;
                default:
                    _updateFailCount = 0;
                    break;
            }

            if (_updateFailCount >= UpdateFailThreshold) {
                logger.Info("Player {guildId} disposed due to state {state}", GuildId, State);
                Shutdown();
                throw new ObjectDisposedException("Player", $"Player disposed due to {State}");
            }
        }
    }
}