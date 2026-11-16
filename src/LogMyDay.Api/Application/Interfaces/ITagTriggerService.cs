using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface ITagTriggerService
{
    Task<IList<TagTriggerResponse>> GetAll(Guid userId);
    Task<TagTriggerResponse> GetById(int id, Guid userId);
    Task<TagTriggerResponse> Create(TagTriggerRequest request, Guid userId);
    Task<TagTriggerResponse> Update(int id, TagTriggerRequest request, Guid userId);

    /// <summary>Deletes the trigger and its reminders that are not done; done ones stay as history.</summary>
    Task Delete(int id, Guid userId);
}
