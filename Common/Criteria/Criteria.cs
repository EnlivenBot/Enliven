using System.Collections.Generic;
using System.Threading.Tasks;

namespace Common.Criteria;

public class Criteria : ICriterion
{
    private readonly List<ICriterion> _criteria = new();

    public Criteria()
    {
    }

    public Criteria(bool needFullMatch)
    {
        NeedFullMatch = needFullMatch;
    }

    public bool NeedFullMatch { get; set; } = true;

    public async Task<bool> JudgeAsync()
    {
        foreach (var criterion in _criteria)
        {
            var result = await criterion.JudgeAsync().ConfigureAwait(false);
            if (NeedFullMatch && !result) return false;
            if (!NeedFullMatch && result) return true;
        }

        return NeedFullMatch;
    }

    /// <summary>
    /// Add criterion to current class
    /// </summary>
    /// <param name="criterion">Target criterion</param>
    /// <returns>Current class</returns>
    public Criteria AddCriterion(ICriterion criterion)
    {
        _criteria.Add(criterion);
        return this;
    }

    /// <summary>
    /// Add a nullable criterion to current class overriding criterion's null handling
    /// </summary>
    /// <param name="criterion">Target criterion</param>
    /// <param name="isNullTrue">Is criterion's null result should be treated as true</param>
    /// <returns>Current class</returns>
    public Criteria AddCriterion(INullableCriterion criterion, bool isNullTrue)
    {
        _criteria.Add(criterion.ToCustom(async nullableCriterion =>
            await nullableCriterion.JudgeNullableAsync() ?? isNullTrue));
        return this;
    }
}