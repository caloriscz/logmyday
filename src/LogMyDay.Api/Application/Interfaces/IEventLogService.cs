using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IEventLogService
{
    Task Log(Guid userId, EventLogLevel level, string message, string? detail = null);
    Task<PagedResult<EventLogResponse>> GetPaged(int pageNumber, int pageSize, Guid userId, bool isAdmin, EventLogLevel? levelFilter = null, string? messageFilter = null, DateTime? dateFrom = null, DateTime? dateTo = null, string sortBy = "time", bool sortDesc = true);
    Task<int> GetCount(Guid userId, EventLogLevel? levelFilter = null, string? messageFilter = null, DateTime? dateFrom = null, DateTime? dateTo = null);
    Task DeleteEvents(Guid userId, int? olderThanDays);
}
