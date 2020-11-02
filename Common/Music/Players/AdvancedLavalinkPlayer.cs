using System;
using System.Diagnostics;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Common.Config;
using Common.History;
using Common.Localization.Entries;
using Common.Music.Controller;
using Lavalink4NET;
using Lavalink4NET.Player;
using NLog;

namespace Common.Music.Players {
    public class AdvancedLavalinkPlayer : LavalinkPlayer {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public readonly HistoryCollection QueueHistory = new HistoryCollection(512, 1000, false);

        public readonly Subject<IEntry> Shutdown = new Subject<IEntry>();
        private GuildConfig? _guildConfig;
        private ulong _lastVoiceChannelId;
        private protected IMusicController _musicController;

        protected AdvancedLavalinkPlayer(IMusicController musicController) {
            _musicController = musicController;
        }

        protected GuildConfig GuildConfig => _guildConfig ??= GuildConfig.Get(GuildId);
        protected BassBoostMode BassBoostMode { get; private set; } = BassBoostMode.Off;
        public bool IsShutdowned { get; private set; }

        public override async Task OnConnectedAsync(VoiceServer voiceServer, VoiceState voiceState) {
            await SetVolumeAsync(GuildConfig.Volume);
            await base.OnConnectedAsync(voiceServer, voiceState);
        }

        public virtual async Task SetVolumeAsync(int volume = 100, bool force = false) {
            volume = volume.Normalize(0, 200);
            await base.SetVolumeAsync((float) volume / 200, force);
            GuildConfig.Volume = volume;
            GuildConfig.Save();
        }

        [Obsolete]
        public override async Task SetVolumeAsync(float volume = 1, bool normalize = false, bool force = false) {
            await SetVolumeAsync((int) (volume * 100), force);
        }

        public virtual void SetBassBoostMode(BassBoostMode mode) {
            BassBoostMode = mode;
        }

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
            logger.Error("Player disposed. Shutdowned: {Shutdowned} Stacktrace: \n{stacktrace}", IsShutdowned, new StackTrace().ToString());
            if (!IsShutdowned) {
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
                    var newPlayer = await _musicController.ProvidePlayer(GuildId, playerShutdownParameters.LastVoiceChannelId, true);
                    newPlayer.Playlist.AddRange(playerShutdownParameters.Playlist!);
                    await newPlayer.PlayAsync(playerShutdownParameters.LastTrack!, playerShutdownParameters.TrackPosition);
                    if (playerShutdownParameters.PlayerState == PlayerState.Paused) {
                        await newPlayer.PauseAsync();
                    }

                    newPlayer.UpdateCurrentTrackIndex();
                    newPlayer.WriteToQueueHistory(new HistoryEntry(
                        new EntryLocalized("Music.ReconnectedAfterDispose", GuildConfig.Prefix, playerShutdownParameters.StoredPlaylist!.Id)));
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

        public virtual Task NodeChanged(LavalinkNode? node = null) {
            return Task.CompletedTask;
        }
    }
}