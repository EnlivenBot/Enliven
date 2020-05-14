using System;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization;
using Bot.Config.Localization.Providers;
using Lavalink4NET;
using Lavalink4NET.Player;

namespace Bot.Music.Players {
    public class AdvancedLavalinkPlayer : LavalinkPlayer {
        protected static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public GuildConfig GuildConfig;
        public readonly ILocalizationProvider Loc;
        public BassBoostMode BassBoostMode = BassBoostMode.Off;
        private int _updateFailCount;
        internal int UpdateFailThreshold = 2;

        public AdvancedLavalinkPlayer(ulong guildId) {
            GuildConfig = GuildConfig.Get(guildId);
            Loc = new GuildLocalizationProvider(GuildConfig);
        }

        public override async Task OnConnectedAsync(VoiceServer voiceServer, VoiceState voiceState) {
            await base.SetVolumeAsync(GuildConfig.Get(GuildId).Volume);
            await base.OnConnectedAsync(voiceServer, voiceState);
        }

        public override async Task SetVolumeAsync(float volume = 1, bool normalize = false) {
            volume = Math.Min(Math.Max(volume, 0), 1.5f);
            await base.SetVolumeAsync(volume, false);
            GuildConfig.Get(GuildId).SetVolume(volume).Save();
        }
        
        public virtual void Shutdown(LocalizedEntry reason, bool needSave = true) {
            Shutdown(reason.Get(Loc), needSave);
        }

        public virtual Task Shutdown(string reason, bool needSave = true) {
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
            logger.Error("Player disposed. Stacktrace: \n{stacktrace}", new System.Diagnostics.StackTrace().ToString());
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