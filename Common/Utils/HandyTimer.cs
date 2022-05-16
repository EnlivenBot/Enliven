using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Utils {
    public class HandyTimer : DisposableBase {
        private TaskCompletionSource<bool> _taskCompletionSource = new();
        private Subject<Unit> _completedSubject = new();
        private bool _taskCompleted;
        public Task TimerElapsed => _taskCompletionSource.Task;
        public IObservable<Unit> OnTimerElapsed => _completedSubject.AsObservable();
        private Timer? _timer;

        public HandyTimer() {
            SetCompleted();
        }

        private void SetCompleted() {
            if (IsDisposed) return;
            if (!_taskCompleted) _completedSubject.OnNext(Unit.Default);
            _taskCompleted = true;
            _taskCompletionSource.TrySetResult(true);
        }

        private void Reset() {
            if (IsDisposed) return;
            if (!_taskCompleted) return;
            _taskCompleted = false;
            _taskCompletionSource = new TaskCompletionSource<bool>();
        }

        public virtual void SetDelay(TimeSpan span) {
            if (span <= TimeSpan.Zero) {
                SetCompleted();
            }
            else {
                Reset();
                _timer?.Dispose();
                _timer = new Timer(_ => SetCompleted(), null, span, TimeSpan.FromMilliseconds(-1));
            }
        }

        public virtual void SetTargetTime(DateTime time) {
            SetDelay(time - DateTime.Now);
        }

        protected override void DisposeInternal() {
            _timer?.Dispose();
            _completedSubject.Dispose();
        }
    }
}