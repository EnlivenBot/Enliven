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
        private const int UpdateFailThreshold = 2;
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

        public event EventHandler<IEntry> Shutdown; 

        public virtual void ExecuteShutdown(EntryLocalized reason, bool needSave = true) {
            ExecuteShutdown(reason.Get(Loc), needSave);
        }

        public virtual Task ExecuteShutdown(string reason, bool needSave = true) {
            IsShutdowned = true;
            // TODO Fix that
            Shutdown.Invoke(this, new EntryString(reason));
            base.Dispose();
            return Task.CompletedTask;
        }

        public virtual Task ExecuteShutdown(bool needSave = true) {
            ExecuteShutdown(Loc.Get("Music.PlaybackStopped"), needSave);
            return Task.CompletedTask;
        }

        /// <summary>
        /// This method is called only from third-party code.
        /// </summary>
        [Obsolete]
        public override void Dispose() {
            if (!IsShutdowned) logger.Error("Player disposed. Stacktrace: \n{stacktrace}", new StackTrace().ToString());
            ExecuteShutdown();
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
                ExecuteShutdown();
                throw new ObjectDisposedException("Player", $"Player disposed due to {State}");
            }
        }
    }
}