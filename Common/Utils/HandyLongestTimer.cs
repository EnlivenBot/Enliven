using System;

namespace Common.Utils {
    public class HandyLongestTimer : HandyTimer {
        private DateTime _targetTime = DateTime.Now;
        public override void SetDelay(TimeSpan span) {
            if (DateTime.Now + span < _targetTime) return;
            _targetTime = DateTime.Now + span;
            base.SetDelay(span);
        }

        public override void Reset() {
            _targetTime = DateTime.Now;
            base.Reset();
        }
    }
}