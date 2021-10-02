using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Common.Config;
using Common.History;
using Common.Localization.Entries;
using Common.Music.Controller;
using Discord;
using Lavalink4NET;
using Lavalink4NET.Events;
using Lavalink4NET.Filters;
using Lavalink4NET.Player;
using NLog;

namespace Common.Music.Players {
    public class AdvancedLavalinkPlayer : LavalinkPlayer {
        public const int MaxEffectsCount = 4;
        
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public readonly HistoryCollection QueueHistory = new HistoryCollection(512, 1000, false);
        private readonly Subject<FilterMapBase> _filtersChanged = new Subject<FilterMapBase>();
        private readonly List<PlayerEffectUse> _effectsList = new List<PlayerEffectUse>();

        public readonly Subject<IEntry> Shutdown = new Subject<IEntry>();
        public readonly Subject<int> VolumeChanged = new Subject<int>();
        public readonly Subject<EnlivenLavalinkClusterNode?> SocketChanged = new Subject<EnlivenLavalinkClusterNode?>();
        public readonly Subject<PlayerState> StateChanged = new Subject<PlayerState>();
        public IObservable<FilterMapBase> FiltersChanged => _filtersChanged.AsObservable();
        private GuildConfig? _guildConfig;
        private ulong _lastVoiceChannelId;
        private protected IMusicController MusicController;
        private IGuildConfigProvider _guildConfigProvider;
        public List<IPlayerDisplay> Displays { get; } = new List<IPlayerDisplay>();
        public ImmutableList<PlayerEffectUse> Effects => _effectsList.ToImmutableList();
        
        protected AdvancedLavalinkPlayer(IMusicController musicController, IGuildConfigProvider guildConfigProvider) {
            _guildConfigProvider = guildConfigProvider;
            MusicController = musicController;
        }

        protected GuildConfig GuildConfig => _guildConfig ??= _guildConfigProvider.Get(GuildId);
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

        private static readonly IEntry ConcatLines = new EntryString("{0}\n{1}");
        private static readonly IEntry ResumeViaPlaylists = new EntryLocalized("Music.ResumeViaPlaylists");
        public virtual async Task ExecuteShutdown(IEntry reason, PlayerShutdownParameters parameters) {
            if (IsShutdowned) return;
            await GetPlayerShutdownParameters(parameters);
            IsShutdowned = true;
            Shutdown.OnNext(reason);
            Shutdown.Dispose();
            MusicController.StoreShutdownParameters(parameters);

            if (parameters.ShutdownDisplays) {
                var body = parameters.SavePlaylist
                    ? ConcatLines.WithArg(reason, ResumeViaPlaylists.WithArg(GuildConfig.Prefix, playerSnapshot.StoredPlaylist!.Id))
                    : reason;
                var header = new EntryLocalized("Music.PlaybackStopped");
                var displayShutdownTasks = Displays.ToList().Select(async display => {
                    try {
                        await display.ExecuteShutdown(header, body);
                    }
                    catch (Exception e) {
                        Logger.Error(e, "Error while shutdowning {DisplayType}", display.GetType().Name);
                    }
                });
                await Task.WhenAll(displayShutdownTasks.ToList());
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
            parameters.Effects = _effectsList.ToList();
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
                    await GetPlayerShutdownParameters(playerShutdownParameters);
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

        public virtual async Task<PlayerEffectUse> ApplyEffect(PlayerEffect effect, IUser? source) {
            if (_effectsList.Count >= MaxEffectsCount) throw new Exception("Maximum number of effects - 5");

            var effectUse = new PlayerEffectUse(source, effect);
            _effectsList.Add(effectUse);

            WriteToQueueHistory(new EntryLocalized("Music.EffectApplied", source?.Username ?? "Unknown", effectUse.Effect.DisplayName));
            await ApplyFilters();
            return effectUse;
        }

        public virtual async Task RemoveEffect(PlayerEffectUse effectUse, IUser? source) {
            if (_effectsList.Remove(effectUse)) {
                await ApplyFilters();
                WriteToQueueHistory(new EntryLocalized("Music.EffectRemoved", source?.Username ?? "Unknown", effectUse.Effect.DisplayName));
            }
        }

        protected async Task ApplyFilters() {
            var effects = _effectsList.SelectMany(use => use.Effect.CurrentFilters)
                .GroupBy(pair => pair.Key)
                .Select(pairs => pairs.First())
                .ToDictionary(pair => pair.Key, pair => pair.Value);

            Filters.Distortion = effects.GetValueOrDefault(DistortionFilterOptions.Name) as DistortionFilterOptions;
            Filters.Equalizer = effects.GetValueOrDefault(EqualizerFilterOptions.Name) as EqualizerFilterOptions;
            Filters.Karaoke = effects.GetValueOrDefault(KaraokeFilterOptions.Name) as KaraokeFilterOptions;
            Filters.Rotation = effects.GetValueOrDefault(RotationFilterOptions.Name) as RotationFilterOptions;
            Filters.Timescale = effects.GetValueOrDefault(TimescaleFilterOptions.Name) as TimescaleFilterOptions;
            Filters.Tremolo = effects.GetValueOrDefault(TremoloFilterOptions.Name) as TremoloFilterOptions;
            Filters.Vibrato = effects.GetValueOrDefault(VibratoFilterOptions.Name) as VibratoFilterOptions;
            Filters.Volume = effects.GetValueOrDefault(VolumeFilterOptions.Name) as VolumeFilterOptions;
            Filters.ChannelMix = effects.GetValueOrDefault(ChannelMixFilterOptions.Name) as ChannelMixFilterOptions;
            Filters.LowPass = effects.GetValueOrDefault(LowPassFilterOptions.Name) as LowPassFilterOptions;
            
            await Filters.CommitAsync();
            _filtersChanged.OnNext(Filters);
        }
    }
}