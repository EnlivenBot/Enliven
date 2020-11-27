using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Utils {
    public class HandyTimer {
        private TaskCompletionSource<bool> _taskCompletionSource = new TaskCompletionSource<bool>();
        private bool _taskCompleted;
        public Task TimerElapsed => _taskCompletionSource.Task;
        private Timer? _timer;

        public HandyTimer() {
            SetCompleted();
        }

        private void SetCompleted() {
            _taskCompleted = true;
            _taskCompletionSource.TrySetResult(true);
        }

        private void Reset() {
            if (!_taskCompleted) return;
            _taskCompleted = false;
            _taskCompletionSource = new TaskCompletionSource<bool>();
        }

        public void SetDelay(TimeSpan span) {
            if (span <= TimeSpan.Zero) {
                SetCompleted();
            }
            else {
                Reset();
                _timer?.Dispose();
                _timer = new Timer(state => SetCompleted(), null, span, TimeSpan.FromMilliseconds(-1));
            }
        }

        public void SetTargetTime(DateTime time) {
            SetDelay(time - DateTime.Now);
        }
    }
}