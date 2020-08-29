using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization.Entries;
using Bot.Config.Localization.Providers;
using Bot.Music;
using Bot.Utilities;
using Bot.Utilities.History;
using Discord;
using HarmonyLib;
using Lavalink4NET;
using Lavalink4NET.Player;
using NLog;
using Tyrrrz.Extensions;

namespace Bot.DiscordRelated.Music {
    public class AdvancedLavalinkPlayer : LavalinkPlayer {
        // ReSharper disable once InconsistentNaming
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();
        protected GuildConfig GuildConfig;
        protected IGuild Guild;
        public readonly ILocalizationProvider Loc;
        protected BassBoostMode BassBoostMode { get; private set; } = BassBoostMode.Off;
        private int _updateFailCount;
        protected ulong _lastVoiceChannelId;
        private const int UpdateFailThreshold = 5;
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
            await base.SetVolumeAsync((float) volume / 100);
            var guildConfig = GuildConfig.Get(GuildId);
            guildConfig.Volume = volume;
            guildConfig.Save();
        }

        [Obsolete]
        public override async Task SetVolumeAsync(float volume = 1, bool normalize = false) {
            await SetVolumeAsync((int) (volume * 100));
        }

        public virtual void SetBassBoostMode(BassBoostMode mode) {
            BassBoostMode = mode;
        }

        public bool IsShutdowned { get; private set; }

        public event EventHandler<IEntry> Shutdown;

        public virtual Task ExecuteShutdown(IEntry reason, bool needSave = true) {
            IsShutdowned = true;
            Shutdown.Invoke(this, reason);
            base.Dispose();
            return Task.CompletedTask;
        }

        public Task ExecuteShutdown(string reason, bool needSave = true) {
            return ExecuteShutdown(new EntryString(reason), needSave);
        }

        public Task ExecuteShutdown(bool needSave = true) {
            return ExecuteShutdown(new EntryLocalized("Music.PlaybackStopped"), needSave);
        }

        public override Task ConnectAsync(ulong voiceChannelId, bool selfDeaf = false, bool selfMute = false) {
            _lastVoiceChannelId = voiceChannelId;
            return base.ConnectAsync(voiceChannelId, selfDeaf, selfMute);
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
        
        public virtual void WriteToQueueHistory(string entry) { }
        public virtual void WriteToQueueHistory(HistoryEntry entry) { }
    }
}