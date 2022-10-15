using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Common.Localization.Entries;

namespace Common.Music.Players {
    public abstract class PlayerDisplayBase : IPlayerDisplay {
        private Subject<IPlayerDisplay> _shutdownObserver = null!;
        public FinalLavalinkPlayer Player { get; private set; } = null!;
        public bool IsInitialized { get; private set; }

        protected PlayerDisplayBase() {
            _shutdownObserver =  new Subject<IPlayerDisplay>();
            Shutdown = _shutdownObserver.AsObservable();
        }

        public virtual Task Initialize(FinalLavalinkPlayer finalLavalinkPlayer) {
            ChangePlayer(finalLavalinkPlayer);
            IsInitialized = true;
            
            return Task.CompletedTask;
        }

        public virtual Task ChangePlayer(FinalLavalinkPlayer newPlayer) {
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
        
        public abstract Task LeaveNotification(IEntry header, IEntry body);

        public bool IsShutdowned { get; private set; }

        public IObservable<IPlayerDisplay> Shutdown { get; }
    }
}