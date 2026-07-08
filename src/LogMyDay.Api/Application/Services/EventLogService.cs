using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

public class EventLogService : IEventLogService
{
    private readonly LogMyDayDbContext _context;
    private readonly ILogger<EventLogService> _logger;

    public EventLogService(LogMyDayDbContext context, ILogger<EventLogService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Log(Guid userId, EventLogLevel level, string message, string? detail = null)
    {
        try
        {
            var entry = new EventLog
            {
                UserId = userId,
                Level = level,
                Message = message.Length > 500 ? message[..500] : message,
                CreatedUtc = DateTime.UtcNow
            };

            _context.EventLogs.Add(entry);

            if (!string.IsNullOrWhiteSpace(detail))
            {
                var detailEntity = new EventLogDetail
                {
                    EventLogId = entry.Id,
                    Description = detail
                };
                entry.Detail = detailEntity;
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write event log for user {UserId}: {Message}", userId, message);
        }
    }

    public async Task<PagedResult<EventLogResponse>> GetPaged(int pageNumber, int pageSize, Guid userId, bool isAdmin, EventLogLevel? levelFilter = null, string? messageFilter = null, DateTime? dateFrom = null, DateTime? dateTo = null, string sortBy = "time", bool sortDesc = true, EventLogCategoryFilter categoryFilter = EventLogCategoryFilter.All)
    {
        var query = _context.EventLogs
            .AsNoTracking()
            .Where(e => e.UserId == userId);

        query = ApplyCategoryFilter(query, categoryFilter);

        if (levelFilter.HasValue)
        {
            query = query.Where(e => e.Level == levelFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(messageFilter))
        {
            query = query.Where(e => EF.Functions.Like(e.Message, $"%{messageFilter}%"));
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(e => e.CreatedUtc >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(e => e.CreatedUtc <= dateTo.Value);
        }

        var totalCount = await query.CountAsync();

        IOrderedQueryable<Domain.Entities.EventLog> ordered = (sortBy?.ToLowerInvariant()) switch
        {
            "level" => sortDesc
                ? query.OrderByDescending(e => e.Level)
                : query.OrderBy(e => e.Level),
            "message" => sortDesc
                ? query.OrderByDescending(e => e.Message)
                : query.OrderBy(e => e.Message),
            _ => sortDesc
                ? query.OrderByDescending(e => e.CreatedUtc)
                : query.OrderBy(e => e.CreatedUtc),
        };

        var items = await ordered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EventLogResponse
            {
                Id = e.Id,
                Level = e.Level.ToString(),
                Message = e.Message,
                CreatedUtc = e.CreatedUtc,
                Detail = isAdmin ? e.Detail!.Description : null
            })
            .ToListAsync();

        return new PagedResult<EventLogResponse>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<int> GetCount(Guid userId, EventLogLevel? levelFilter = null, string? messageFilter = null, DateTime? dateFrom = null, DateTime? dateTo = null, EventLogCategoryFilter categoryFilter = EventLogCategoryFilter.All)
    {
        var query = _context.EventLogs
            .AsNoTracking()
            .Where(e => e.UserId == userId);

        query = ApplyCategoryFilter(query, categoryFilter);

        if (levelFilter.HasValue)
        {
            query = query.Where(e => e.Level == levelFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(messageFilter))
        {
            query = query.Where(e => EF.Functions.Like(e.Message, $"%{messageFilter}%"));
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(e => e.CreatedUtc >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(e => e.CreatedUtc <= dateTo.Value);
        }

        return await query.CountAsync();
    }

    // Categories are message-prefix conventions, not a database column: synced mobile diagnostics
    // arrive as "[category] body"; other events use stable "Activity/Reminder/Todo list …"
    // prefixes. "\" escapes "[" so the pattern stays literal on SQL Server as well as SQLite.
    private static IQueryable<EventLog> ApplyCategoryFilter(IQueryable<EventLog> query, EventLogCategoryFilter categoryFilter)
    {
        return categoryFilter switch
        {
            EventLogCategoryFilter.ReminderDiag => query.Where(e => EF.Functions.Like(e.Message, @"\[reminder-diag]%", @"\")),
            EventLogCategoryFilter.NoDiagnostics => query.Where(e => !EF.Functions.Like(e.Message, @"\[%", @"\")),
            EventLogCategoryFilter.Activity => query.Where(e => EF.Functions.Like(e.Message, "Activity %")),
            EventLogCategoryFilter.Reminder => query.Where(e => EF.Functions.Like(e.Message, "Reminder %")),
            EventLogCategoryFilter.TodoList => query.Where(e => EF.Functions.Like(e.Message, "Todo list %")),
            _ => query
        };
    }

    public async Task DeleteEvents(Guid userId, int? olderThanDays)
    {
        var query = _context.EventLogs.Where(e => e.UserId == userId);

        if (olderThanDays.HasValue)
        {
            var cutoff = DateTime.UtcNow.AddDays(-olderThanDays.Value);
            query = query.Where(e => e.CreatedUtc < cutoff);
        }

        await query.ExecuteDeleteAsync();
    }
}