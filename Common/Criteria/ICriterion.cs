using System.Threading.Tasks;

namespace Common.Criteria {
    public interface ICriterion {
        Task<bool> JudgeAsync();
    }
}