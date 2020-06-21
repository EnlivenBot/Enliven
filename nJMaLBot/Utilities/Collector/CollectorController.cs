using System;
using System.Threading;

namespace Bot.Utilities.Collector {
    public class CollectorController {
        public event EventHandler<CollectorEventArgsBase>? RemoveArgsFailed;
        private Timer _timer = null!;

        public void SetTimeout(TimeSpan timeout) {
            _timer = new Timer(state => { Dispose(); }, null, timeout, TimeSpan.FromSeconds(0));
        }

        public event EventHandler? Stop;

        public void Dispose() {
            Stop?.Invoke(null, EventArgs.Empty);
            _timer?.Dispose();
        }

        public virtual void OnRemoveArgsFailed(CollectorEventArgsBase e) {
            RemoveArgsFailed?.Invoke(this, e);
        }
    }

    public enum CollectorFilter {
        Off,
        IgnoreSelf,
        IgnoreBots
    }
}