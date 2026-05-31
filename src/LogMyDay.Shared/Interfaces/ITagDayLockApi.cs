using LogMyDay.Shared.DTOs;
using Refit;

namespace LogMyDay.Shared.Interfaces;

public interface ITagDayLockApi
{
    [Get("/api/tag-day-locks")]
    Task<IList<TagDayLockResponse>> GetForDate([AliasAs("date")] string date);

    [Post("/api/tag-day-locks")]
    Task<TagDayLockResponse> Upsert([Body] TagDayLockRequest request);

    [Delete("/api/tag-day-locks")]
    Task Delete([AliasAs("tagId")] int tagId, [AliasAs("date")] string date);
}
