using LogMyDay.Shared.DTOs;
using Refit;

namespace LogMyDay.Shared.Interfaces;

public interface ITagTriggerApi
{
    [Get("/api/tag-triggers")]
    Task<IList<TagTriggerResponse>> GetTagTriggers();

    [Get("/api/tag-triggers/{id}")]
    Task<TagTriggerResponse> GetTagTriggerById(int id);

    [Post("/api/tag-triggers")]
    Task<TagTriggerResponse> CreateTagTrigger([Body] TagTriggerRequest request);

    [Put("/api/tag-triggers/{id}")]
    Task<TagTriggerResponse> UpdateTagTrigger(int id, [Body] TagTriggerRequest request);

    [Delete("/api/tag-triggers/{id}")]
    Task DeleteTagTrigger(int id);
}
