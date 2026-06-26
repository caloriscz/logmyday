using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IReminderService
{
    Task<IList<ReminderResponse>> GetAll(Guid userId, DateOnly? date = null);
    Task<ReminderResponse> Create(ReminderRequest request, Guid userId);
    Task Update(int id, ReminderRequest request, Guid userId);
    Task Delete(int id, Guid userId);
    Task<ReminderResponse> Complete(int id, ReminderCompleteRequest request, Guid userId);
    Task<ReminderResponse> Reopen(int id, Guid userId);
    Task<ReminderResponse> Skip(int id, Guid userId, DateOnly? date = null);
    Task<ReminderResponse> Unskip(int id, Guid userId, DateOnly? date = null);
    Task Reorder(IList<ReminderReorderRequest> items, Guid userId);
}
