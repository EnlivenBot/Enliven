using System;
using System.Threading.Tasks;
using Bot.DiscordRelated.Criteria;

namespace Bot.Utilities {
    public class TimeChecker {
        private CustomCriterion _customCriterion = null!;

        public TimeChecker(TimeSpan timeout) : this(timeout, DateTime.Now) { }

        public TimeChecker(TimeSpan timeout, DateTime lastTime) {
            Timeout = timeout;
            LastTime = lastTime;
        }

        public TimeSpan Timeout { get; set; }
        public DateTime LastTime { get; set; }

        public bool IsTimeoutPassed => LastTime + Timeout < DateTime.Now;

        public void Update(DateTime? targetTime = null) {
            LastTime = targetTime ?? DateTime.Now;
        }

        public ICriterion ToCriterion() {
            _customCriterion ??= new CustomCriterion(() => Task.FromResult(IsTimeoutPassed));
            return _customCriterion;
        }
    }
}