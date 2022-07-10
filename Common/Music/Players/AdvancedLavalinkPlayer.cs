using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
using Lavalink4NET.Filters;
using Lavalink4NET.Player;
using NLog;
using Tyrrrz.Extensions;

namespace Common.Music.Players {
    public class AdvancedLavalinkPlayer : WrappedLavalinkPlayer {
        public const int MaxEffectsCount = 4;
        
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public readonly HistoryCollection QueueHistory = new HistoryCollection(512, 1000, false);
        private readonly Subject<FilterMapBase> _filtersChanged = new Subject<FilterMapBase>();
        private readonly List<PlayerEffectUse> _effectsList = new List<PlayerEffectUse>();
        private readonly TaskCompletionSource<PlayerSnapshot> _shutdownTaskCompletionSource = new TaskCompletionSource<PlayerSnapshot>();

        public Task<PlayerSnapshot> ShutdownTask => _shutdownTaskCompletionSource.Task;
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
        public bool IsShutdowned => _shutdownTaskCompletionSource.Task.IsCompleted;

        public override async Task OnConnectedAsync(VoiceServer voiceServer, VoiceState voiceState) {
            await SetVolumeAsync(GuildConfig.Volume);
            await base.OnConnectedAsync(voiceServer, voiceState);
        }

        public override async Task SetVolumeAsync(int volume = 100, bool force = false) {
            await base.SetVolumeAsync(volume, force);
            GuildConfig.Volume = (int)(Volume * 200);
            GuildConfig.Save();
        }

        private bool _isShutdownRequested;
        private static readonly IEntry ConcatLines = new EntryString("{0}\n{1}");
        private static readonly IEntry ResumeViaPlaylists = new EntryLocalized("Music.ResumeViaPlaylists");
        private static readonly IEntry PlaybackStopped = new EntryLocalized("Music.PlaybackStopped");
        public virtual async Task Shutdown(IEntry reason, PlayerShutdownParameters parameters) {
            if (IsShutdowned || _isShutdownRequested) return;
            _isShutdownRequested = true;

            var playerSnapshot = await GetPlayerSnapshot(parameters);
            if (!_shutdownTaskCompletionSource.TrySetResult(playerSnapshot)) return;

            try {
                MusicController.StoreSnapshot(playerSnapshot);

                if (parameters.ShutdownDisplays) {
                    var body = parameters.SavePlaylist
                        ? ConcatLines.WithArg(reason, ResumeViaPlaylists.WithArg(playerSnapshot.StoredPlaylist!.Id))
                        : reason;
                    var displayShutdownTasks = Displays.ToList().Select(async display => {
                        try {
                            await display.ExecuteShutdown(PlaybackStopped, body);
                        }
                        catch (Exception e) {
                            Logger.Error(e, "Error while shutdowning {DisplayType}", display.GetType().Name);
                        }
                    });
                    await Task.WhenAll(displayShutdownTasks.ToList());
                }
            }
            finally {
                await base.DisposeAsyncCore();
            }
        }

        public Task Shutdown(string reason, PlayerShutdownParameters parameters) {
            return Shutdown(new EntryString(reason), parameters);
        }

        public Task Shutdown(PlayerShutdownParameters parameters) {
            return Shutdown(new EntryLocalized("Music.PlaybackStopped"), parameters);
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
        
        public virtual void WriteToQueueHistory(IEnumerable<HistoryEntry> entries) {
            QueueHistory.AddRange(entries);
        }

        protected virtual Task<PlayerSnapshot> GetPlayerSnapshot(PlayerSnapshotParameters parameters) {
            return Task.FromResult(new PlayerSnapshot {
                GuildId = GuildId,
                LastVoiceChannelId = _lastVoiceChannelId,
                LastTrack = CurrentTrack,
                TrackPosition = Position.Position,
                PlayerState = State,
                Effects = _effectsList.ToList()
            });
        }

        public virtual async Task ApplyStateSnapshot(PlayerStateSnapshot playerSnapshot) {
            if (playerSnapshot.LastTrack != null) await PlayAsync(playerSnapshot.LastTrack, playerSnapshot.TrackPosition);
            if (playerSnapshot.PlayerState == PlayerState.Paused) await PauseAsync();
            
            _effectsList.Clear();
            foreach (var playerEffectUse in playerSnapshot.Effects) {
                _effectsList.Add(new PlayerEffectUse(playerEffectUse.User, playerEffectUse.Effect));
            }
            await ApplyFiltersAsync();
        }

        private static readonly IEntry PlaybackStoppedEntry = new EntryLocalized("Music.PlaybackStopped");
        private static readonly IEntry TryReconnectAfterDisposeEntry = new EntryLocalized("Music.TryReconnectAfterDispose");
        private static readonly PlayerShutdownParameters ParametersForDisposedPlayer = new() {ShutdownDisplays = false, SavePlaylist = true};
        
        /// <remarks>
        /// We don't call Dispose or DisposeAsync on our side of the player.
        /// If Dispose was called on the player, something happened in the Lavalink and our job is to try to restart the player
        /// </remarks>
        protected sealed override void Dispose(bool disposing) {
            if (IsShutdowned || !disposing || _isShutdownRequested) return;
            DisposeAsyncCore().GetAwaiter().GetResult();
        }

        /// <remarks>
        /// We don't call Dispose or DisposeAsync on our side of the player.
        /// If Dispose was called on the player, something happened in the Lavalink and our job is to try to restart the player
        /// </remarks>
        protected override async ValueTask DisposeAsyncCore() {
            Logger.Warn("Got player in {GuildId} dispose request\n{StackTrace}", GuildId, new StackTrace());
            if (IsShutdowned || _isShutdownRequested) return;
            try {
                Logger.Warn("Shutdowning player in {GuildId} due to Dispose call", GuildId);
                await Shutdown(TryReconnectAfterDisposeEntry, ParametersForDisposedPlayer);
                var playerSnapshot = await ShutdownTask;

                var notificationUpdateTasks = Displays
                    .ToList()
                    .Select(display => LeaveNotificationToDisplay(display, playerSnapshot));
                await Task.WhenAll(notificationUpdateTasks);

                MusicController.OnPlayerDisposed(this, playerSnapshot, QueueHistory.ToList());
            }
            catch (Exception e) {
                Logger.Error(e, "Error while disposing player");
            }
            
            Task LeaveNotificationToDisplay(IPlayerDisplay display, PlayerSnapshot snapshot)
                => display.LeaveNotification(PlaybackStoppedEntry, TryReconnectAfterDisposeEntry.WithArg(snapshot.StoredPlaylist!.Id));
        }

        public virtual async Task<PlayerEffectUse> ApplyEffect(PlayerEffect effect, IUser? source) {
            if (_effectsList.Count >= MaxEffectsCount) throw new Exception("Maximum number of effects - 5");

            var effectUse = new PlayerEffectUse(source, effect);
            _effectsList.Add(effectUse);

            WriteToQueueHistory(new EntryLocalized("Music.EffectApplied", source?.Username ?? "Unknown", effectUse.Effect.DisplayName));
            await ApplyFiltersAsync();
            return effectUse;
        }

        public virtual async Task RemoveEffect(PlayerEffectUse effectUse, IUser? source) {
            if (_effectsList.Remove(effectUse)) {
                await ApplyFiltersAsync();
                WriteToQueueHistory(new EntryLocalized("Music.EffectRemoved", source?.Username ?? "Unknown", effectUse.Effect.DisplayName));
            }
        }

        protected async Task ApplyFiltersAsync() {
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