using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Common.Config;
using Common.History;
using Common.Localization.Entries;
using Common.Music.Cluster;
using Common.Music.Players.Options;
using Discord;
using Lavalink4NET.Filters;
using Lavalink4NET.InactivityTracking.Players;
using Lavalink4NET.InactivityTracking.Trackers;
using Lavalink4NET.Players;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Tyrrrz.Extensions;

namespace Common.Music.Players;

public class AdvancedLavalinkPlayer : WrappedLavalinkPlayer, IPlayerOnReady, IPlayerShutdownInternally, IInactivityPlayerListener
{
    protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly List<PlayerEffectUse> _effectsList = new();
    private readonly IEnlivenClusterAudioService _enlivenClusterAudioService;
    private readonly Subject<IPlayerFilters> _filtersChanged = new();
    private readonly IGuildConfigProvider _guildConfigProvider;
    private readonly TaskCompletionSource<PlayerSnapshot> _shutdownTaskCompletionSource = new();
    private readonly IEnumerable<PlayerEffectUse>? _startupPlayerEffects;

    protected readonly IServiceScope ServiceScope;
    private GuildConfig? _guildConfig;

    private bool _isShutdownRequested;

    protected AdvancedLavalinkPlayer(IPlayerProperties<AdvancedLavalinkPlayer, AdvancedLavalinkPlayerOptions> options) :
        base(options)
    {
        ServiceScope = options.ServiceProvider!.CreateScope();
        _guildConfigProvider = ServiceScope.ServiceProvider.GetRequiredService<IGuildConfigProvider>();
        _enlivenClusterAudioService = ServiceScope.ServiceProvider.GetRequiredService<IEnlivenClusterAudioService>();

        _startupPlayerEffects = options.Options.Value.PlayerEffects;
    }

    public HistoryCollection QueueHistory { get; } = new(512, 1000, false);

    public IObservable<IPlayerFilters> FiltersChanged => _filtersChanged.AsObservable();

    public HashSet<IPlayerDisplay> Displays { get; } = [];

    public IReadOnlyList<PlayerEffectUse> Effects => _effectsList;

    protected GuildConfig GuildConfig => _guildConfig ??= _guildConfigProvider.Get(GuildId);

    public async Task OnReady()
    {
        if (_startupPlayerEffects is not null)
        {
            foreach (var playerEffectUse in _startupPlayerEffects)
            {
                _effectsList.Add(playerEffectUse);
            }

            await ApplyFiltersAsync();
        }
    }

    public override async ValueTask SetVolumeAsync(int volume, CancellationToken token = new())
    {
        await base.SetVolumeAsync(volume, token);
        GuildConfig.Volume = (int)(Volume * 200);
        GuildConfig.Save();
    }

    public virtual void WriteToQueueHistory(string entry)
    {
        WriteToQueueHistory(new HistoryEntry(new EntryString(entry)));
    }

    public virtual void WriteToQueueHistory(IEntry entry)
    {
        WriteToQueueHistory(entry is HistoryEntry historyEntry ? historyEntry : new HistoryEntry(entry));
    }

    public virtual void WriteToQueueHistory(HistoryEntry entry)
    {
        QueueHistory.Add(entry);
    }

    public virtual void WriteToQueueHistory(IEnumerable<HistoryEntry> entries)
    {
        QueueHistory.AddRange(entries);
    }

    protected virtual ValueTask FillPlayerSnapshot(PlayerSnapshot snapshot)
    {
        snapshot.GuildId = GuildId;
        snapshot.LastVoiceChannelId = VoiceChannelId;
        snapshot.TrackPosition = Position?.Position;
        snapshot.PlayerState = State;
        snapshot.Effects = _effectsList.ToList();
        snapshot.Volume = Volume;

        return ValueTask.CompletedTask;
    }

    public virtual async Task<PlayerEffectUse> ApplyEffect(PlayerEffect effect, IUser? source)
    {
        if (_effectsList.Count >= PlayerConstants.MaxEffectsCount) throw new Exception("Maximum number of effects - 5");

        var effectUse = new PlayerEffectUse(source, effect);
        _effectsList.Add(effectUse);

        WriteToQueueHistory(new EntryLocalized("Music.EffectApplied", source?.Username ?? "Unknown",
            effectUse.Effect.DisplayName));
        await ApplyFiltersAsync();
        return effectUse;
    }

    public virtual async Task RemoveEffect(PlayerEffectUse effectUse, IUser? source)
    {
        if (_effectsList.Remove(effectUse))
        {
            await ApplyFiltersAsync();
            WriteToQueueHistory(new EntryLocalized("Music.EffectRemoved", source?.Username ?? "Unknown",
                effectUse.Effect.DisplayName));
        }
    }

    protected async Task ApplyFiltersAsync()
    {
        var effects = _effectsList.SelectMany(use => use.Effect.CurrentFilters)
            .GroupBy(pair => pair.Key)
            .Select(pairs => pairs.First())
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        Filters.Distortion = effects.GetValueOrDefault(nameof(DistortionFilterOptions)) as DistortionFilterOptions;
        Filters.Equalizer = effects.GetValueOrDefault(nameof(EqualizerFilterOptions)) as EqualizerFilterOptions;
        Filters.Karaoke = effects.GetValueOrDefault(nameof(KaraokeFilterOptions)) as KaraokeFilterOptions;
        Filters.Rotation = effects.GetValueOrDefault(nameof(RotationFilterOptions)) as RotationFilterOptions;
        Filters.Timescale = effects.GetValueOrDefault(nameof(TimescaleFilterOptions)) as TimescaleFilterOptions;
        Filters.Tremolo = effects.GetValueOrDefault(nameof(TremoloFilterOptions)) as TremoloFilterOptions;
        Filters.Vibrato = effects.GetValueOrDefault(nameof(VibratoFilterOptions)) as VibratoFilterOptions;
        Filters.Volume = effects.GetValueOrDefault(nameof(VolumeFilterOptions)) as VolumeFilterOptions;
        Filters.ChannelMix = effects.GetValueOrDefault(nameof(ChannelMixFilterOptions)) as ChannelMixFilterOptions;
        Filters.LowPass = effects.GetValueOrDefault(nameof(LowPassFilterOptions)) as LowPassFilterOptions;

        await Filters.CommitAsync();
        _filtersChanged.OnNext(Filters);
    }

    #region Disposing/Shutdowning
    
    public async ValueTask NotifyPlayerInactiveAsync(PlayerTrackingState trackingState, CancellationToken cancellationToken = default)
    {
        await Shutdown(new EntryLocalized("Music.NoListenersLeft"),
            new PlayerShutdownParameters { RestartPlayer = false, ShutdownDisplays = true, SavePlaylist = true });
    }

    /// <remarks>
    /// We don't call Dispose or DisposeAsync on our side of the player.
    /// If Dispose was called on the player, something happened in the Lavalink and our job is to try to restart the player
    /// </remarks>
    protected override async ValueTask DisposeAsyncCore()
    {
        if (State == PlayerState.Destroyed || _isShutdownRequested) return;
        Logger.Warn("Got player in {GuildId} dispose request\n{StackTrace}", GuildId, new StackTrace());
        await Shutdown(new EntryLocalized("Music.PlaybackStopped"),
            new PlayerShutdownParameters() { RestartPlayer = true, SavePlaylist = true, ShutdownDisplays = false });
    }

    public Task Shutdown(IEntry reason, PlayerShutdownParameters parameters)
    {
        return _enlivenClusterAudioService.ShutdownPlayer(this, parameters, reason);
    }

    public Task Shutdown(PlayerShutdownParameters parameters) =>
        Shutdown(new EntryLocalized("Music.PlaybackStopped"), parameters);

    async Task<PlayerSnapshot> IPlayerShutdownInternally.ShutdownInternal()
    {
        _isShutdownRequested = true;
        var playerSnapshot = new PlayerSnapshot();
        try
        {
            await FillPlayerSnapshot(playerSnapshot);
        }
        finally
        {
            try
            {
                await base.DisposeAsyncCore();
            }
            catch (Exception)
            {
                // Manually disconnects if node not available anymore
                await DiscordClient
                    .SendVoiceUpdateAsync(GuildId, null)
                    .ConfigureAwait(false);
            }
            ServiceScope.Dispose();
        }

        return playerSnapshot;
    }

    #endregion

    public ValueTask NotifyPlayerActiveAsync(PlayerTrackingState trackingState, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask NotifyPlayerTrackedAsync(PlayerTrackingState trackingState, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}