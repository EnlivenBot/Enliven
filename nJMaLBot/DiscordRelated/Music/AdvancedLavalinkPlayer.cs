using System;
using System.Diagnostics;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Bot.Config;
using Bot.Config.Localization.Entries;
using Bot.Config.Localization.Providers;
using Bot.Music;
using Bot.Utilities;
using Bot.Utilities.History;
using Discord;
using Lavalink4NET;
using Lavalink4NET.Player;
using NLog;

namespace Bot.DiscordRelated.Music {
    public class AdvancedLavalinkPlayer : LavalinkPlayer {
        // ReSharper disable once InconsistentNaming
        protected internal static readonly Logger logger = LogManager.GetCurrentClassLogger();
        protected GuildConfig GuildConfig;
        protected IGuild Guild;
        public readonly ILocalizationProvider Loc;
        protected BassBoostMode BassBoostMode { get; private set; } = BassBoostMode.Off;
        private int _updateFailCount;
        private ulong _lastVoiceChannelId;
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

        public readonly Subject<IEntry> Shutdown = new Subject<IEntry>();

        public virtual Task ExecuteShutdown(IEntry reason, PlayerShutdownParameters parameters) {
            GetPlayerShutdownParameters(parameters);
            IsShutdowned = true;
            Shutdown.OnNext(reason);
            Shutdown.Dispose();
            base.Dispose();
            return Task.CompletedTask;
        }

        public Task ExecuteShutdown(string reason, PlayerShutdownParameters parameters) {
            return ExecuteShutdown(new EntryString(reason), parameters);
        }

        public Task ExecuteShutdown(PlayerShutdownParameters parameters) {
            return ExecuteShutdown(new EntryLocalized("Music.PlaybackStopped"), parameters);
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
                ExecuteShutdown(new PlayerShutdownParameters());
                throw new ObjectDisposedException("Player", $"Player disposed due to {State}");
            }
        }

        public virtual void WriteToQueueHistory(string entry) { }
        public virtual void WriteToQueueHistory(HistoryEntry entry) { }

        public virtual void GetPlayerShutdownParameters(PlayerShutdownParameters parameters) {
            parameters.LastVoiceChannelId = _lastVoiceChannelId;
            parameters.LastTrack = CurrentTrack;
            parameters.TrackPosition = TrackPosition;
            parameters.PlayerState = State;
        }

        /// <summary>
        /// This method is called only from third-party code.
        /// </summary>
        [Obsolete]
        public override async void Dispose() {
            if (!IsShutdowned) {
                logger.Error("Player disposed. Stacktrace: \n{stacktrace}", new StackTrace().ToString());
                try {

                    var playerShutdownParameters = new PlayerShutdownParameters {AddResumeToMessage = false};
                    GetPlayerShutdownParameters(playerShutdownParameters);
                    await ExecuteShutdown(new EntryLocalized("Music.TryReconnectAfterDispose", GuildConfig.Prefix, playerShutdownParameters.StoredPlaylist!.Id),
                        playerShutdownParameters);
                    if (State != PlayerState.Destroyed) {
                        base.Dispose();
                    }
                    logger.Info("Old player state - {State}", State);
                    await Task.Delay(3000);
                    var newPlayer = await PlayersController.ProvidePlayer(GuildId, playerShutdownParameters.LastVoiceChannelId, true);
                    newPlayer.Playlist.AddRange(playerShutdownParameters.Playlist!);
                    await newPlayer.PlayAsync(playerShutdownParameters.LastTrack!, playerShutdownParameters.TrackPosition);
                    if (playerShutdownParameters.PlayerState == PlayerState.Paused) {
                        await newPlayer.PauseAsync();
                    }

                    newPlayer.UpdateCurrentTrackIndex();
                    newPlayer.WriteToQueueHistory(new HistoryEntry(
                        new EntryLocalized("Music.ReconnectedAfterDispose", GuildConfig.Prefix, playerShutdownParameters.StoredPlaylist!.Id)));
                    await newPlayer.EnqueueControlMessageSend(playerShutdownParameters.LastControlMessage!.Channel);
                    logger.Info("New player state - {State}", newPlayer.State);
                    logger.Info("Track - {Track}: {Position}", newPlayer.CurrentTrack, newPlayer.TrackPosition);
                }
                catch (Exception e) {
                    logger.Error(e, "Error while resuming player");
                }
            }
            else {
                base.Dispose();
            }
        }
    }
}