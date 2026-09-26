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

    /// <summary>What recomputing the rule over the range would change. Without dates the range
    /// is the first source date to today.</summary>
    [Get("/api/tag-rules/{id}/preview")]
    Task<TagRuleRangeResponse> PreviewTagRule(int id, [Query(Format = "yyyy-MM-dd")] DateOnly? from = null, [Query(Format = "yyyy-MM-dd")] DateOnly? to = null);

    [Post("/api/tag-rules/{id}/recompute")]
    Task<TagRuleRangeResponse> RecomputeTagRule(int id, [Body] TagRuleRecomputeRequest request);
}
