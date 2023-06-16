using System;
using System.Threading;
using Common.Utils;

namespace Bot.Utilities.Collector {
    public class CollectorController : DisposableBase {
        public event EventHandler<CollectorEventArgsBase>? RemoveArgsFailed;
        private Timer? _timer;

        public void SetTimeout(TimeSpan timeout) {
            if (_timer == null)
                _timer = new Timer(state => { Dispose(); }, null, timeout, TimeSpan.FromSeconds(0));
            else
                _timer.Change(timeout, TimeSpan.FromSeconds(0));
        }

        protected override void DisposeInternal() {
            _timer?.Dispose();
        }

        public virtual void OnRemoveArgsFailed(CollectorEventArgsBase e) {
            RemoveArgsFailed?.Invoke(this, e);
        }
    }
}