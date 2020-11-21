using System.Threading.Tasks;

namespace Bot.DiscordRelated.Criteria {
    public interface ICriterion {
        Task<bool> JudgeAsync();
    }
}