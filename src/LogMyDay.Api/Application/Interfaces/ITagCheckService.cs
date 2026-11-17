using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface ITagCheckService
{
    Task<IList<TagCheckResponse>> GetAll(Guid userId);
    Task<TagCheckResponse> GetById(int id, Guid userId);
    Task<TagCheckResponse> Create(TagCheckRequest request, Guid userId);
    Task<TagCheckResponse> Update(int id, TagCheckRequest request, Guid userId);
    Task Delete(int id, Guid userId);

    /// <summary>The days in the range that break an active check, newest first. Defaults: the
    /// last 30 days up to today. The range ends today at the latest and spans at most a year.</summary>
    Task<TagCheckReviewResponse> Review(DateOnly? from, DateOnly? to, Guid userId);
}
