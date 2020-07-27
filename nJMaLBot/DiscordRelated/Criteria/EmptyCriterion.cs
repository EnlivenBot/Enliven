using System.Threading.Tasks;

namespace Bot.DiscordRelated.Criteria {
    public class EmptyCriterion : ICriterion {
        public Task<bool> JudgeAsync() {
            return Task.FromResult(true);
        }
    }
}