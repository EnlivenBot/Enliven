using System;

namespace Common.Utils {
    public class HandyShortestTimer : HandyTimer {
        private DateTime _targetTime = DateTime.MaxValue;
        public override void SetDelay(TimeSpan span) {
            if (DateTime.Now + span >= _targetTime) return;
            _targetTime = DateTime.Now + span;
            base.SetDelay(span);
        }

        public override void Reset() {
            _targetTime = DateTime.MaxValue;
            base.Reset();
        }
    }
}