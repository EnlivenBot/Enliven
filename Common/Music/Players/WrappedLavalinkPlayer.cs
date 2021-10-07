using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Lavalink4NET.Events;
using Lavalink4NET.Player;

namespace Common.Music.Players {
    public class WrappedLavalinkPlayer : LavalinkPlayer {
        private readonly Subject<int> _volumeChanged = new();
        public IObservable<int> VolumeChanged => _volumeChanged.AsObservable();
        private readonly Subject<EnlivenLavalinkClusterNode?> _socketChanged = new();
        public IObservable<EnlivenLavalinkClusterNode?> SocketChanged => _socketChanged.AsObservable();
        private readonly Subject<PlayerState> _stateChanged = new();
        public IObservable<PlayerState> StateChanged => _stateChanged.AsObservable();
        public virtual async Task SetVolumeAsync(int volume = 100, bool force = false) {
            volume = volume.Normalize(0, 200);
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (Volume != (float) volume / 200 || force) {
                await base.SetVolumeAsync((float) volume / 200, false, force);
                _volumeChanged.OnNext(volume);
            }
        }
        
        [Obsolete]
        public override async Task SetVolumeAsync(float volume = 1, bool normalize = false, bool force = false) {
            await SetVolumeAsync((int) (volume * 200), force);
        }
        
        public override Task OnSocketChanged(SocketChangedEventArgs eventArgs)
        {
            _socketChanged.OnNext(eventArgs.NewSocket as EnlivenLavalinkClusterNode);
            return base.OnSocketChanged(eventArgs);
        }
        
        public override async Task OnTrackEndAsync(TrackEndEventArgs eventArgs) {
            await base.OnTrackEndAsync(eventArgs);
            _stateChanged.OnNext(State);
        }
        
        public override async Task OnTrackExceptionAsync(TrackExceptionEventArgs eventArgs) {
            await base.OnTrackExceptionAsync(eventArgs);
            _stateChanged.OnNext(State);
        }
        
        public override async Task OnTrackStartedAsync(TrackStartedEventArgs eventArgs) {
            await base.OnTrackStartedAsync(eventArgs);
            _stateChanged.OnNext(State);
        }
        
        public override async Task OnTrackStuckAsync(TrackStuckEventArgs eventArgs) {
            await base.OnTrackStuckAsync(eventArgs);
            _stateChanged.OnNext(State);
        }
        
        public override async Task PauseAsync() {
            await base.PauseAsync();
            _stateChanged.OnNext(State);
        }

        public override async Task ResumeAsync() {
            await base.ResumeAsync();
            _stateChanged.OnNext(State);
        }
    }
}