using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IEventLogService
{
    Task Log(Guid userId, EventLogLevel level, string message, string? detail = null);
    Task<PagedResult<EventLogResponse>> GetPaged(int pageNumber, int pageSize, Guid userId, bool isAdmin, EventLogLevel? levelFilter = null);
    Task<int> GetCount(Guid userId, EventLogLevel? levelFilter = null);
}
