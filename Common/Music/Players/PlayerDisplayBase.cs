using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Common.Localization.Entries;

namespace Common.Music.Players {
    public abstract class PlayerDisplayBase : IPlayerDisplay {
        public FinalLavalinkPlayer Player { get; set; } = null!;

        public virtual Task Initialize(FinalLavalinkPlayer finalLavalinkPlayer) {
            ChangePlayer(finalLavalinkPlayer);
            
            return Task.CompletedTask;
        }

        public virtual Task ChangePlayer(FinalLavalinkPlayer newPlayer) {
            // ReSharper disable once ConstantConditionalAccessQualifier
            Player?.Displays.Remove(this);
            Player = newPlayer;
            Player.Displays.Add(this);
            
            return Task.CompletedTask;
        }

        public virtual Task Shutdown(IEntry header, IEntry body) {
            Player?.Displays.Remove(this);
            
            return Task.CompletedTask;
        }
        
        public abstract Task LeaveNotification(IEntry header, IEntry body);

        public ISubject<IPlayerDisplay> Disposed { get; set; } = new Subject<IPlayerDisplay>();

        public virtual void Dispose() {
            Disposed.OnNext(this);
            
            (Disposed as IDisposable)?.Dispose();
        }
    }
}