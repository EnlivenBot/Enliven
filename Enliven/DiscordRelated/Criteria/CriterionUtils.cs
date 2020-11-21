using System;
using System.Threading.Tasks;

namespace Bot.DiscordRelated.Criteria {
    public static class CriterionUtils {
        public static CustomCriterion ToCustom<T>(this T criterion, Func<T, Task<bool>>? transform = null) where T : ICriterion{
            return new CustomCriterion(async () => {
                if (transform == null) {
                    return await criterion.JudgeAsync();
                }

                return await transform(criterion);
            });
        }

        public static Criteria ToCriteria(this ICriterion criterion) {
            if (criterion is Criteria criteria) {
                return criteria;
            }
            return new Criteria().AddCriterion(criterion);
        }
        
        public static ICriterion Invert(this ICriterion criterion) {
            return new CustomCriterion(async () => !await criterion.JudgeAsync());
        }
    }
}