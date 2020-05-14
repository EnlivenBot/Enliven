using System;
using Timer = System.Threading.Timer;

namespace Bot.Utilities.Collector {
    public class CollectorController {
        private Timer _timer;

        public void SetTimeout(TimeSpan timeout) {
            _timer = new Timer(state => { Dispose(); }, null, timeout, TimeSpan.FromSeconds(0));
        }

        public event EventHandler Stop;

        public void Dispose() {
            Stop?.Invoke(null, EventArgs.Empty);
            _timer?.Dispose();
        }
    }

    public enum CollectorFilter {
        Off,
        IgnoreSelf,
        IgnoreBots
    }
}