namespace LogMyDay.Shared.Interfaces;

using LogMyDay.Shared.DTOs;
using Refit;

public interface IEventLogApi
{
    [Get("/api/eventlogs")]
    Task<PagedResult<EventLogResponse>> GetEventLogs(int pageNumber = 1, int pageSize = 50, string? level = null);

    [Get("/api/eventlogs/count")]
    Task<int> GetCount(string? level = null);
}
