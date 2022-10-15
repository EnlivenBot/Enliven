using System;
using System.Threading.Tasks;
using Common.Localization.Entries;

namespace Common.Music.Players {
    public interface IPlayerDisplay {
        public FinalLavalinkPlayer Player { get; }

        public Task Initialize(FinalLavalinkPlayer finalLavalinkPlayer);

        public Task ChangePlayer(FinalLavalinkPlayer newPlayer);

        public Task ExecuteShutdown(IEntry header, IEntry body);

        public Task LeaveNotification(IEntry header, IEntry body);
        
        public bool IsShutdowned { get; }
        public IObservable<IPlayerDisplay> Shutdown { get; }
        bool IsInitialized { get; }
    }
}