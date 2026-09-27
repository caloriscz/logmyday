using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface ITagRuleService
{
    Task<IList<TagRuleResponse>> GetAll(Guid userId);
    Task<TagRuleResponse> GetById(int id, Guid userId);

    /// <summary>Creates the rule, marks its target tag computed and evaluates today. The rule is
    /// active from today (forward only); history is recomputed on request, not on save.</summary>
    Task<TagRuleResponse> Create(TagRuleRequest request, Guid userId);

    /// <summary>Saves the rule and re-evaluates its active range (EffectiveFrom to today). The
    /// target tag cannot change.</summary>
    Task<TagRuleResponse> Update(int id, TagRuleRequest request, Guid userId);

    /// <summary>Deletes the rule and every result it generated, and clears the target's
    /// computed flag.</summary>
    Task Delete(int id, Guid userId);
}
