using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IReminderListService
{
    Task<IList<ReminderListResponse>> GetAll(Guid userId, DateOnly? date = null);
    Task<ReminderListResponse> GetById(int id, Guid userId);
    Task<ReminderListResponse> Create(ReminderListRequest request, Guid userId);
    Task Update(int id, ReminderListRequest request, Guid userId);
    Task Delete(int id, Guid userId);
}
