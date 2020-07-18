using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bot.DiscordRelated.Criteria {
    public class Criteria : ICriterion {
        private readonly List<ICriterion> _criteria = new List<ICriterion>();

        public bool NeedFullMatch { get; set; }

        public Criteria AddCriterion(ICriterion criterion) {
            _criteria.Add(criterion);
            return this;
        }

        public async Task<bool> JudgeAsync() {
            foreach (var criterion in _criteria)
            {
                var result = await criterion.JudgeAsync().ConfigureAwait(false);
                if (NeedFullMatch && !result) return false;
                if (!NeedFullMatch && result) return true;
            }
            return NeedFullMatch;
        }
    }
}