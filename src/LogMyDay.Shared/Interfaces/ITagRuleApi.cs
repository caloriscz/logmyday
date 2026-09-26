using LogMyDay.Shared.DTOs;
using Refit;

namespace LogMyDay.Shared.Interfaces;

public interface ITagRuleApi
{
    [Get("/api/tag-rules")]
    Task<IList<TagRuleResponse>> GetTagRules();

    [Get("/api/tag-rules/{id}")]
    Task<TagRuleResponse> GetTagRuleById(int id);

    [Post("/api/tag-rules")]
    Task<TagRuleResponse> CreateTagRule([Body] TagRuleRequest request);

    [Put("/api/tag-rules/{id}")]
    Task<TagRuleResponse> UpdateTagRule(int id, [Body] TagRuleRequest request);

    [Delete("/api/tag-rules/{id}")]
    Task DeleteTagRule(int id);
}
