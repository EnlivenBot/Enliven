using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Common.Config;
using Common.History;
using Common.Localization.Entries;
using Common.Music.Controller;
using Lavalink4NET;
using Lavalink4NET.Events;
using Lavalink4NET.Player;
using NLog;

namespace Common.Music.Players {
    public class AdvancedLavalinkPlayer : LavalinkPlayer {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public readonly HistoryCollection QueueHistory = new HistoryCollection(512, 1000, false);

        public readonly Subject<IEntry> Shutdown = new Subject<IEntry>();
        public readonly Subject<int> VolumeChanged = new Subject<int>();
        public readonly Subject<BassBoostMode> BassboostChanged = new Subject<BassBoostMode>();
        public readonly Subject<EnlivenLavalinkClusterNode?> SocketChanged = new Subject<EnlivenLavalinkClusterNode?>();
        public readonly Subject<PlayerState> StateChanged = new Subject<PlayerState>();
        private GuildConfig? _guildConfig;
        private ulong _lastVoiceChannelId;
        private protected IMusicController MusicController;
        private IGuildConfigProvider _guildConfigProvider;
        public List<IPlayerDisplay> Displays { get; } = new List<IPlayerDisplay>();

        protected AdvancedLavalinkPlayer(IMusicController musicController, IGuildConfigProvider guildConfigProvider) {
            _guildConfigProvider = guildConfigProvider;
            MusicController = musicController;
        }

        protected GuildConfig GuildConfig => _guildConfig ??= _guildConfigProvider.Get(GuildId);
        public BassBoostMode BassBoostMode { get; private set; } = BassBoostMode.Off;
        public bool IsShutdowned { get; private set; }

        public override async Task OnConnectedAsync(VoiceServer voiceServer, VoiceState voiceState) {
            await SetVolumeAsync(GuildConfig.Volume);
            await base.OnConnectedAsync(voiceServer, voiceState);
        }

        public virtual async Task SetVolumeAsync(int volume = 100, bool force = false) {
            volume = volume.Normalize(0, 200);
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (Volume != (float) volume / 200 || force) {
                await base.SetVolumeAsync((float) volume / 200, force);
                VolumeChanged.OnNext(volume);
                GuildConfig.Volume = volume;
                GuildConfig.Save();
            }
        }

        [Obsolete]
        public override async Task SetVolumeAsync(float volume = 1, bool normalize = false, bool force = false) {
            await SetVolumeAsync((int) (volume * 200), force);
        }

        public virtual void SetBassBoost(BassBoostMode mode) {
            BassBoostMode = mode;
            var bands = new List<EqualizerBand>();
            switch (mode) {
                case BassBoostMode.Off:
                    bands.Add(new EqualizerBand(0, 0f));
                    bands.Add(new EqualizerBand(1, 0f));
                    break;
                case BassBoostMode.Low:
                    bands.Add(new EqualizerBand(0, 0.25f));
                    bands.Add(new EqualizerBand(1, 0.15f));
                    break;
                case BassBoostMode.Medium:
                    bands.Add(new EqualizerBand(0, 0.5f));
                    bands.Add(new EqualizerBand(1, 0.25f));
                    break;
                case BassBoostMode.High:
                    bands.Add(new EqualizerBand(0, 0.75f));
                    bands.Add(new EqualizerBand(1, 0.5f));
                    break;
                case BassBoostMode.Extreme:
                    bands.Add(new EqualizerBand(0, 1f));
                    bands.Add(new EqualizerBand(1, 0.75f));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            UpdateEqualizerAsync(bands, false, true);
            BassboostChanged.OnNext(mode);
        }

        public virtual async Task ExecuteShutdown(IEntry reason, PlayerShutdownParameters parameters) {
            GetPlayerShutdownParameters(parameters);
            IsShutdowned = true;
            Shutdown.OnNext(reason);
            Shutdown.Dispose();
            MusicController.StoreShutdownParameters(parameters);

            if (parameters.ShutdownDisplays) {
                foreach (var playerDisplay in Displays.ToList()) {
                    var body = parameters.NeedSave
                        ? new EntryString("{0}\n{1}", reason, new EntryLocalized("Music.ResumeViaPlaylists", GuildConfig.Prefix, parameters.StoredPlaylist!.Id))
                        : reason;
                    await playerDisplay.ExecuteShutdown(new EntryLocalized("Music.PlaybackStopped"), body);
                }
            }

            base.Dispose();
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

        public virtual void WriteToQueueHistory(string entry) {
            WriteToQueueHistory(new HistoryEntry(new EntryString(entry)));
        }

        public virtual void WriteToQueueHistory(IEntry entry) {
            WriteToQueueHistory(entry is HistoryEntry historyEntry ? historyEntry : new HistoryEntry(entry));
        }

        public virtual void WriteToQueueHistory(HistoryEntry entry) {
            QueueHistory.Add(entry);
        }

        public virtual Task GetPlayerShutdownParameters(PlayerShutdownParameters parameters) {
            parameters.GuildId = GuildId;
            parameters.LastVoiceChannelId = _lastVoiceChannelId;
            parameters.LastTrack = CurrentTrack;
            parameters.TrackPosition = TrackPosition;
            parameters.PlayerState = State;
            return Task.CompletedTask;
        }

        /// <summary>
        /// This method is called only from third-party code.
        /// </summary>
        [Obsolete]
        public override async void Dispose() {
            if (!IsShutdowned) {
                try {
                    var playerShutdownParameters = new PlayerShutdownParameters {ShutdownDisplays = false, NeedSave = true};
                    GetPlayerShutdownParameters(playerShutdownParameters);
                    var reason = new EntryLocalized("Music.TryReconnectAfterDispose", GuildConfig.Prefix, playerShutdownParameters.StoredPlaylist!.Id);
                    await ExecuteShutdown(reason, playerShutdownParameters);
                    foreach (var playerDisplay in Displays.ToList()) await playerDisplay.LeaveNotification(new EntryLocalized("Music.PlaybackStopped"), reason);
                    if (State != PlayerState.Destroyed) {
                        base.Dispose();
                    }
                    
                    await Task.Delay(2000);
                    var newPlayer = (await MusicController.RestoreLastPlayer(GuildId))!;
                    foreach (var playerDisplay in Displays.ToList()) await playerDisplay.ChangePlayer(newPlayer);
                    newPlayer.WriteToQueueHistory(new HistoryEntry(
                        new EntryLocalized("Music.ReconnectedAfterDispose", GuildConfig.Prefix, playerShutdownParameters.StoredPlaylist!.Id)));
                }
                catch (Exception e) {
                    Logger.Error(e, "Error while resuming player");
                }
            }
        }

        public override Task OnSocketChanged(SocketChangedEventArgs eventArgs)
        {
            SocketChanged.OnNext(eventArgs.NewSocket as EnlivenLavalinkClusterNode);
            return base.OnSocketChanged(eventArgs);
        }

        public override async Task OnTrackEndAsync(TrackEndEventArgs eventArgs) {
            await base.OnTrackEndAsync(eventArgs);
            StateChanged.OnNext(State);
        }

        public override async Task OnTrackExceptionAsync(TrackExceptionEventArgs eventArgs) {
            await base.OnTrackExceptionAsync(eventArgs);
            StateChanged.OnNext(State);
        }

        public override async Task OnTrackStartedAsync(TrackStartedEventArgs eventArgs) {
            await base.OnTrackStartedAsync(eventArgs);
            StateChanged.OnNext(State);
        }

        public override async Task OnTrackStuckAsync(TrackStuckEventArgs eventArgs) {
            await base.OnTrackStuckAsync(eventArgs);
            StateChanged.OnNext(State);
        }

        public override async Task PauseAsync() {
            await base.PauseAsync();
            StateChanged.OnNext(State);
        }

        public override async Task ResumeAsync() {
            await base.ResumeAsync();
            StateChanged.OnNext(State);
        }
    }
}