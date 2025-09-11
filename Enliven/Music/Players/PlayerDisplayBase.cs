using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Common.Localization.Entries;

namespace Bot.Music.Players;

public abstract class PlayerDisplayBase : IPlayerDisplay {
    private Subject<IPlayerDisplay> _shutdownObserver = null!;

    protected PlayerDisplayBase() {
        _shutdownObserver = new Subject<IPlayerDisplay>();
        Shutdown = _shutdownObserver.AsObservable();
    }

    public EnlivenLavalinkPlayer? Player { get; private set; }
    public bool IsInitialized { get; private set; }

    public virtual async Task Initialize(EnlivenLavalinkPlayer enlivenLavalinkPlayer) {
        await ChangePlayer(enlivenLavalinkPlayer);
        IsInitialized = true;
    }

    public virtual Task ChangePlayer(EnlivenLavalinkPlayer newPlayer) {
        // ReSharper disable once ConstantConditionalAccessQualifier
        Player?.Displays.Remove(this);
        Player = newPlayer;
        Player.Displays.Add(this);

        return Task.CompletedTask;
    }

    public virtual Task ExecuteShutdown(IEntry header, IEntry body) {
        Player?.Displays.Remove(this);
        IsShutdowned = true;
        _shutdownObserver.OnNext(this);
        _shutdownObserver.OnCompleted();
        return Task.CompletedTask;
    }

    public bool IsShutdowned { get; private set; }

    public IObservable<IPlayerDisplay> Shutdown { get; }
}