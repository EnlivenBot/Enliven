using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Common.Localization.Entries;

namespace Common.Music.Players {
    public interface IPlayerDisplay : IDisposable {
        public FinalLavalinkPlayer Player { get; set; }

        public Task Initialize(FinalLavalinkPlayer finalLavalinkPlayer);
        
        public Task Shutdown(IEntry header, IEntry body);

        public Task LeaveNotification(IEntry header, IEntry body);

        public ISubject<IPlayerDisplay> Disposed { get; set; }

        void IDisposable.Dispose() {
            Disposed?.Subscribe();
        }
    }
}