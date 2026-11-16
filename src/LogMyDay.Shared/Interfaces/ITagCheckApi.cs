using LogMyDay.Shared.DTOs;
using Refit;

namespace LogMyDay.Shared.Interfaces;

public interface ITagCheckApi
{
    [Get("/api/tag-checks")]
    Task<IList<TagCheckResponse>> GetTagChecks();

    [Get("/api/tag-checks/{id}")]
    Task<TagCheckResponse> GetTagCheckById(int id);

    [Post("/api/tag-checks")]
    Task<TagCheckResponse> CreateTagCheck([Body] TagCheckRequest request);

    [Put("/api/tag-checks/{id}")]
    Task<TagCheckResponse> UpdateTagCheck(int id, [Body] TagCheckRequest request);

    [Delete("/api/tag-checks/{id}")]
    Task DeleteTagCheck(int id);

    /// <summary>The days that break an active check. Without dates: the last 30 days up to today.</summary>
    [Get("/api/tag-checks/review")]
    Task<TagCheckReviewResponse> ReviewTagChecks([Query(Format = "yyyy-MM-dd")] DateOnly? from = null, [Query(Format = "yyyy-MM-dd")] DateOnly? to = null);
}
