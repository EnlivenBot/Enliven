using System.Threading.Tasks;

namespace Bot.DiscordRelated.Criteria {
    public interface INullableCriterion : ICriterion {
        bool IsNullableTrue { get; set; }

        async Task<bool> ICriterion.JudgeAsync() {
            return await JudgeNullableAsync() ?? IsNullableTrue;
        }

        Task<bool?> JudgeNullableAsync();
    }
}