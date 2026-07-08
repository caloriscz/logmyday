namespace LogMyDay.Shared.Interfaces;

using LogMyDay.Shared.DTOs;
using Refit;

public interface IEventLogApi
{
    [Post("/api/eventlogs")]
    Task LogEvent([Body] EventLogRequest request);

    [Get("/api/eventlogs")]
    Task<PagedResult<EventLogResponse>> GetEventLogs(int pageNumber = 1, int pageSize = 50, string? level = null, string? message = null, DateTime? dateFrom = null, DateTime? dateTo = null, string sortBy = "time", bool sortDesc = true, EventLogCategoryFilter category = EventLogCategoryFilter.All);

    [Get("/api/eventlogs/count")]
    Task<int> GetCount(string? level = null, string? message = null, DateTime? dateFrom = null, DateTime? dateTo = null, EventLogCategoryFilter category = EventLogCategoryFilter.All);

    [Delete("/api/eventlogs")]
    Task DeleteEventLogs(int? olderThanDays = null);
}
