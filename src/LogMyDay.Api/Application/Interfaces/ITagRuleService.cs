using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface ITagRuleService
{
    Task<IList<TagRuleResponse>> GetAll(Guid userId);
    Task<TagRuleResponse> GetById(int id, Guid userId);

    /// <summary>Creates the rule, marks its target tag computed and evaluates today. The rule is
    /// active from today (forward only); history is recomputed on request, not on save.</summary>
    Task<TagRuleResponse> Create(TagRuleRequest request, Guid userId);

    /// <summary>Saves the rule and re-evaluates today. Earlier days keep their values until the
    /// user recomputes a range (the editor offers it with a preview). The target tag cannot change.</summary>
    Task<TagRuleResponse> Update(int id, TagRuleRequest request, Guid userId);

    /// <summary>What recomputing the range would change. Defaults: from the first source date,
    /// to today.</summary>
    Task<TagRuleRangeResponse> Preview(int id, DateOnly? from, DateOnly? to, Guid userId);

    /// <summary>Recomputes the range and moves EffectiveFrom back to its start when earlier, so
    /// the recomputed days stay up to date from now on. One event log summary per run.</summary>
    Task<TagRuleRangeResponse> Recompute(int id, TagRuleRecomputeRequest request, Guid userId);

    /// <summary>Deletes the rule and every result it generated, and clears the target's
    /// computed flag.</summary>
    Task Delete(int id, Guid userId);
}
