using System;
using System.Threading.Tasks;

namespace Common.Criteria {
    public class CustomCriterion : ICriterion {
        private Func<Task<bool>> _func;
        public CustomCriterion(Func<Task<bool>> func) {
            _func = func;
        }

        public Task<bool> JudgeAsync() {
            return _func();
        }
    }
}