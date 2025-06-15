using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Application.Services;

public class ActivityService : IActivityService
{
    private readonly LogMyDayDbContext _context;

    public ActivityService(LogMyDayDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }    public async Task<ActivityResponse> Create(ActivityRequest calendarRequest)
    {
        // Get the tag to check if it's repeatable and what its time granularity is
        var tag = await _context.Tags.FindAsync(calendarRequest.PrimaryTagId);
        if (tag == null)
        {
            throw new ArgumentException("Invalid tag ID");
        }

        // Check if tag is not repeatable and there's already an activity for this time granularity
        if (!tag.IsRepeatable && tag.TimeGranularity != TimeGranularity.Exact)
        {
            if (await HasActivityForTimeGranularity(tag.Id, calendarRequest.DateStarted))
            {
                throw new InvalidOperationException($"An activity for this tag already exists for the selected {tag.TimeGranularity.ToString().ToLower()} period. This tag is not repeatable.");
            }
        }

        var activity = new Activity
        {
            DateStarted = calendarRequest.DateStarted,
            DateFinished = calendarRequest.DateFinished,
            DateCreated = DateTime.UtcNow,
            Description = calendarRequest.Description,
            TagId = calendarRequest.PrimaryTagId ?? 0,

        };

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        // Reload with tags included for full response
        activity = await _context.Activities
              .Include(ct => ct.Tag)
              .ThenInclude(t => t.InputType)
              .FirstAsync(c => c.Id == activity.Id);

        return MapToResponse(activity);
    }

    public async Task<bool> Delete(int id)
    {
        var activity = await _context.Activities.FindAsync(id);
        if (activity == null)
        {
            return false;
        }
        _context.Activities.Remove(activity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<ActivityResponse>> GetAll()
    {
        var activities = await _context.Activities
               .Include(ct => ct.Tag)
               .ThenInclude(t => t.InputType)
               .ToListAsync();

        return activities.Select(MapToResponse).ToList();
    }    public async Task<PagedResult<ActivityResponse>> GetPaged(int pageNumber, int pageSize, string orderBy, int? tagId = null, DateTime? startDate = null, DateTime? endDate = null, string? descriptionFilter = null)
    {
        var query = _context.Activities
            .Include(ct => ct.Tag)
            .ThenInclude(t => t.InputType)
            .AsQueryable();

        if (tagId.HasValue)
        {
            query = query.Where(a => a.TagId == tagId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(a => a.DateStarted >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.DateStarted <= endDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(descriptionFilter))
        {
            query = query.Where(a => a.Description != null && a.Description.Contains(descriptionFilter));
        }

        // Order by date
        if (orderBy?.ToLower() == "asc")
            query = query.OrderBy(a => a.DateStarted);
        else
            query = query.OrderByDescending(a => a.DateStarted);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<ActivityResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }    public async Task<PagedResult<ActivityResponse>> GetPagedByWeeks(int weekPageNumber, int weeksPerPage, string orderBy, int? tagId = null, DateTime? startDate = null, DateTime? endDate = null, string? descriptionFilter = null)
    {
        var query = _context.Activities
            .Include(ct => ct.Tag)
            .ThenInclude(t => t.InputType)
            .AsQueryable();

        if (tagId.HasValue)
        {
            query = query.Where(a => a.TagId == tagId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(a => a.DateStarted >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.DateStarted <= endDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(descriptionFilter))
        {
            query = query.Where(a => a.Description != null && a.Description.Contains(descriptionFilter));
        }

        // Get all activities first to calculate week ranges
        var allActivities = await query.ToListAsync();
        
        if (!allActivities.Any())
        {
            return new PagedResult<ActivityResponse>
            {
                Items = new List<ActivityResponse>(),
                TotalCount = 0,
                PageNumber = weekPageNumber,
                PageSize = weeksPerPage
            };
        }

        // Group activities by week and sort weeks
        var weekGroups = allActivities
            .GroupBy(a => GetStartOfWeek(a.DateStarted))
            .OrderBy(g => g.Key)
            .ToList();

        if (orderBy?.ToLower() == "desc")
        {
            weekGroups = weekGroups.OrderByDescending(g => g.Key).ToList();
        }

        // Calculate pagination for weeks
        var totalWeeks = weekGroups.Count;
        var weekGroupsToTake = weekGroups
            .Skip((weekPageNumber - 1) * weeksPerPage)
            .Take(weeksPerPage)
            .ToList();

        // Get all activities from the selected weeks
        var weekStartDates = weekGroupsToTake.Select(g => g.Key).ToList();
        var activitiesInSelectedWeeks = allActivities
            .Where(a => weekStartDates.Contains(GetStartOfWeek(a.DateStarted)))
            .ToList();        // Order activities within the selected weeks
        if (orderBy?.ToLower() == "asc")
            activitiesInSelectedWeeks = activitiesInSelectedWeeks.OrderBy(a => a.DateStarted).ToList();
        else
            activitiesInSelectedWeeks = activitiesInSelectedWeeks.OrderByDescending(a => a.DateStarted).ToList();

        return new PagedResult<ActivityResponse>
        {
            Items = activitiesInSelectedWeeks.Select(MapToResponse).ToList(),
            TotalCount = totalWeeks, // Total number of weeks
            PageNumber = weekPageNumber,
            PageSize = weeksPerPage
        };
    }    public async Task<PagedResult<ActivityResponse>> GetPagedByMonths(int monthPageNumber, int monthsPerPage, string orderBy, int? tagId = null, DateTime? startDate = null, DateTime? endDate = null, string? descriptionFilter = null)
    {
        var query = _context.Activities
            .Include(ct => ct.Tag)
            .ThenInclude(t => t.InputType)
            .AsQueryable();

        if (tagId.HasValue)
        {
            query = query.Where(a => a.TagId == tagId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(a => a.DateStarted >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.DateStarted <= endDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(descriptionFilter))
        {
            query = query.Where(a => a.Description != null && a.Description.Contains(descriptionFilter));
        }

        // Get all activities first to calculate month ranges
        var allActivities = await query.ToListAsync();
        
        if (!allActivities.Any())
        {
            return new PagedResult<ActivityResponse>
            {
                Items = new List<ActivityResponse>(),
                TotalCount = 0,
                PageNumber = monthPageNumber,
                PageSize = monthsPerPage
            };
        }

        // Group activities by month and sort months
        var monthGroups = allActivities
            .GroupBy(a => new DateTime(a.DateStarted.Year, a.DateStarted.Month, 1))
            .OrderBy(g => g.Key)
            .ToList();

        if (orderBy?.ToLower() == "desc")
        {
            monthGroups = monthGroups.OrderByDescending(g => g.Key).ToList();
        }

        // Calculate pagination for months
        var totalMonths = monthGroups.Count;
        var monthGroupsToTake = monthGroups
            .Skip((monthPageNumber - 1) * monthsPerPage)
            .Take(monthsPerPage)
            .ToList();

        // Get all activities from the selected months
        var monthStartDates = monthGroupsToTake.Select(g => g.Key).ToList();
        var activitiesInSelectedMonths = allActivities
            .Where(a => monthStartDates.Contains(new DateTime(a.DateStarted.Year, a.DateStarted.Month, 1)))
            .ToList();        // Order activities within the selected months
        if (orderBy?.ToLower() == "asc")
            activitiesInSelectedMonths = activitiesInSelectedMonths.OrderBy(a => a.DateStarted).ToList();
        else
            activitiesInSelectedMonths = activitiesInSelectedMonths.OrderByDescending(a => a.DateStarted).ToList();

        return new PagedResult<ActivityResponse>
        {
            Items = activitiesInSelectedMonths.Select(MapToResponse).ToList(),
            TotalCount = totalMonths, // Total number of months
            PageNumber = monthPageNumber,
            PageSize = monthsPerPage
        };
    }    public Task<List<ActivityResponse>> GetByDate(ActivityRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<ActivityResponse> GetById(int id)
    {
        var activity = await _context.Activities
                      .Include(ct => ct.Tag)
                      .ThenInclude(t => t.InputType)
                      .SingleOrDefaultAsync(c => c.Id == id)
                      ?? throw new KeyNotFoundException("Activity not found");

        return MapToResponse(activity);
    }

    public Task<ActivityResponse> Update(int id, DateTime dateCreated, DateTime? dateFinished)
    {
        throw new NotImplementedException();
    }    private ActivityResponse MapToResponse(Activity calendar)
    {
        var primaryTag = calendar;

        return new ActivityResponse
        {
            Id = calendar.Id,
            DateCreated = calendar.DateCreated,
            DateStarted = calendar.DateStarted,
            Description = calendar.Description ?? string.Empty,
            DateFinished = calendar.DateFinished,
            PrimaryTagId = primaryTag?.TagId,
            PrimaryTagName = primaryTag?.Tag?.TagName ?? string.Empty,
            PrimaryTagValue = calendar.Description ?? string.Empty, // Using description as the value
            ElementId = primaryTag?.Tag?.InputType?.Id,
            ElementName = primaryTag?.Tag?.InputType?.Name ?? string.Empty,
            TagRequired = primaryTag?.Tag?.IsRequired ?? false
        };
    }private DateTime GetStartOfWeek(DateTime date)
    {
        // Assuming Monday is the start of the week
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-1 * diff).Date;
    }

    public async Task<bool> HasActivityForTimeGranularity(int tagId, DateTime dateStarted)
    {
        var tag = await _context.Tags.FindAsync(tagId);
        if (tag == null || tag.TimeGranularity == TimeGranularity.Exact)
        {
            return false; // No validation for Exact granularity
        }

        DateTime startRange, endRange;

        // Determine the date range based on time granularity
        switch (tag.TimeGranularity)
        {
            case TimeGranularity.Daily:
                startRange = dateStarted.Date;
                endRange = startRange.AddDays(1).AddTicks(-1);
                break;
            case TimeGranularity.Hourly:
                startRange = new DateTime(dateStarted.Year, dateStarted.Month, dateStarted.Day, dateStarted.Hour, 0, 0);
                endRange = startRange.AddHours(1).AddTicks(-1);
                break;
            case TimeGranularity.Weekly:
                startRange = GetStartOfWeek(dateStarted);
                endRange = startRange.AddDays(7).AddTicks(-1);
                break;
            case TimeGranularity.Monthly:
                startRange = new DateTime(dateStarted.Year, dateStarted.Month, 1);
                endRange = startRange.AddMonths(1).AddTicks(-1);
                break;
            case TimeGranularity.Yearly:
                startRange = new DateTime(dateStarted.Year, 1, 1);
                endRange = startRange.AddYears(1).AddTicks(-1);
                break;
            default:
                return false;
        }

        // Check if there's already an activity for this tag in the specified range
        return await _context.Activities
            .Where(a => a.TagId == tagId && a.DateStarted >= startRange && a.DateStarted <= endRange)
            .AnyAsync();
    }
}
