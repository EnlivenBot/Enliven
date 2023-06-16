using System.Threading.Tasks;

namespace Common.Criteria {
    public class EmptyCriterion : ICriterion {
        public Task<bool> JudgeAsync() {
            return Task.FromResult(true);
        }
    }
}