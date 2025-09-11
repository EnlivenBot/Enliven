using System;
using System.Threading.Tasks;
using Common.Localization.Entries;

namespace Bot.Music.Players;

public interface IPlayerDisplay {
    public EnlivenLavalinkPlayer? Player { get; }

    public bool IsShutdowned { get; }
    public IObservable<IPlayerDisplay> Shutdown { get; }
    bool IsInitialized { get; }

    public Task Initialize(EnlivenLavalinkPlayer enlivenLavalinkPlayer);

    public Task ChangePlayer(EnlivenLavalinkPlayer newPlayer);

    public Task ExecuteShutdown(IEntry header, IEntry body);
}