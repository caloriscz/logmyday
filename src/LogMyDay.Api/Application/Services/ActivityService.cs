using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Api.Infrastructure.Specifications;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Application.Services;

public class ActivityService : IActivityService
{
    private readonly LogMyDayDbContext _context;
    private readonly IActivityRepository _activityRepository;

    public ActivityService(LogMyDayDbContext context, IActivityRepository activityRepository)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _activityRepository = activityRepository ?? throw new ArgumentNullException(nameof(activityRepository));
    }

    public async Task<ActivityResponse> Create(ActivityRequest calendarRequest, Guid userId)
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
            if (await HasActivityForTimeGranularity(tag.Id, calendarRequest.DateStarted, userId))
            {
                throw new InvalidOperationException(
                    $"An activity for this tag already exists for the selected {tag.TimeGranularity.ToString().ToLower()} period. This tag is not repeatable."
                );
            }
        }

        var activity = new Activity
        {
            DateStarted = calendarRequest.DateStarted,
            DateFinished = calendarRequest.DateFinished,
            DateCreated = DateTime.UtcNow,
            Description = calendarRequest.Description,
            TagId = calendarRequest.PrimaryTagId ?? 0,
            UserId = userId,
        };

        await _activityRepository.AddAsync(activity);
        await _activityRepository.SaveChangesAsync();

        // Reload with tags included for full response - use direct query for single activity
        var reloadedActivity = await _context
            .Activities
            .Include(ct => ct.Tag)
            .ThenInclude(t => t.InputType)
            .Include(ct => ct.Tag)
            .ThenInclude(t => t.Group)
            .FirstAsync(c => c.Id == activity.Id);

        return MapToResponse(reloadedActivity);
    }

    public async Task<bool> Delete(int id, Guid userId)
    {
        var spec = new ActivityByIdAndUserSpec(id, userId);
        var activity = await _activityRepository.GetSingleAsync(spec);

        if (activity == null)
        {
            return false;
        }

        await _activityRepository.DeleteAsync(activity);
        await _activityRepository.SaveChangesAsync();

        return true;
    }

    public async Task<List<ActivityResponse>> GetAll(Guid userId)
    {
        var spec = new ActivitiesForUserSpec(userId);
        var activities = await _activityRepository.GetAsync(spec);

        return activities.Select(MapToResponse).ToList();
    }

    public async Task<PagedResult<ActivityResponse>> GetPaged(
        int pageNumber,
        int pageSize,
        string orderBy,
        Guid userId,
        int? tagId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? descriptionFilter = null
    )
    {
        var spec = new PagedActivitiesSpec(
            userId,
            pageNumber,
            pageSize,
            orderBy,
            tagId,
            startDate,
            endDate,
            descriptionFilter
        );

        var items = await _activityRepository.GetAsync(spec);

        // Get total count with same filters but no paging
        var countSpec = new PagedActivitiesSpec(
            userId,
            1,
            int.MaxValue,
            orderBy,
            tagId,
            startDate,
            endDate,
            descriptionFilter
        );
        var totalCount = await _activityRepository.CountAsync(countSpec);

        return new PagedResult<ActivityResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
    }

    public async Task<PagedResult<ActivityResponse>> GetPagedByWeeks(
        int weekPageNumber,
        int weeksPerPage,
        string orderBy,
        Guid userId,
        int? tagId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? descriptionFilter = null
    )
    {
        var baseQuery = _context.Activities.Include(ct => ct.Tag).ThenInclude(t => t.InputType).Include(ct => ct.Tag).ThenInclude(t => t.Group).Where(a => a.UserId == userId).AsQueryable();
        baseQuery = ApplyActivityFilters(baseQuery, tagId, startDate, endDate, descriptionFilter);

        return await GetPagedByTimePeriod(
            baseQuery,
            weekPageNumber,
            weeksPerPage,
            orderBy,
            date => EF.Functions.DateDiffWeek(new DateTime(1900, 1, 1), date),
            "week"
        );
    }

    public async Task<PagedResult<ActivityResponse>> GetPagedByMonths(
        int monthPageNumber,
        int monthsPerPage,
        string orderBy,
        Guid userId,
        int? tagId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? descriptionFilter = null
    )
    {
        var baseQuery = _context.Activities.Include(ct => ct.Tag).ThenInclude(t => t.InputType).Include(ct => ct.Tag).ThenInclude(t => t.Group).Where(a => a.UserId == userId).AsQueryable();
        baseQuery = ApplyActivityFilters(baseQuery, tagId, startDate, endDate, descriptionFilter);

        return await GetPagedByTimePeriod(
            baseQuery,
            monthPageNumber,
            monthsPerPage,
            orderBy,
            date => EF.Functions.DateDiffMonth(new DateTime(1900, 1, 1), date),
            "month"
        );
    }

    public Task<List<ActivityResponse>> GetByDate(ActivityRequest request, Guid userId)
    {
        throw new NotImplementedException();
    }

    public async Task<ActivityResponse> GetById(int id, Guid userId)
    {
        var spec = new ActivityByIdAndUserSpec(id, userId);
        var activity = await _activityRepository.GetSingleAsync(spec) ?? throw new KeyNotFoundException("Activity not found");

        return MapToResponse(activity);
    }

    public async Task<ActivityResponse> Update(int id, ActivityRequest request, Guid userId)
    {
        var spec = new ActivityByIdAndUserSpec(id, userId);
        var activity = await _activityRepository.GetSingleAsync(spec) ?? throw new KeyNotFoundException("Activity not found");
        var tagId = request.PrimaryTagId ?? activity.TagId;
        var tag = await _context.Tags.FindAsync(tagId);

        if (tag == null)
        {
            throw new ArgumentException("Invalid tag ID");
        }

        if (!tag.IsRepeatable && tag.TimeGranularity != TimeGranularity.Exact)
        {
            if (await HasActivityForTimeGranularity(tag.Id, request.DateStarted, userId, excludeActivityId: id))
            {
                throw new InvalidOperationException(
                    $"An activity for this tag already exists for the selected {tag.TimeGranularity.ToString().ToLower()} period. This tag is not repeatable."
                );
            }
        }

        activity.TagId = tag.Id;
        activity.DateStarted = request.DateStarted;
        activity.DateFinished = tag.IsRange ? request.DateFinished : null;
        activity.Description = request.Description;

        await _activityRepository.UpdateAsync(activity);
        await _activityRepository.SaveChangesAsync();

        await _context.Entry(activity).Reference(a => a.Tag).LoadAsync();

        if (activity.Tag is not null)
        {
            await _context.Entry(activity.Tag).Reference(t => t.InputType).LoadAsync();
        }

        return MapToResponse(activity);
    }

    private ActivityResponse MapToResponse(Activity calendar)
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
            PrimaryTagName = primaryTag?.Tag?.Group?.Name != null ? $"{primaryTag.Tag.Group.Name}: {primaryTag.Tag.TagName}" : primaryTag?.Tag?.TagName ?? string.Empty,
            PrimaryTagValue = calendar.Description ?? string.Empty,
            ElementId = primaryTag?.Tag?.InputType?.Id,
            ElementName = primaryTag?.Tag?.InputType?.Name ?? string.Empty,
            TagRequired = primaryTag?.Tag?.IsRequired ?? false,
        };
    }

    private DateTime GetStartOfWeek(DateTime date)
    {
        int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;

        return date.AddDays(-1 * diff).Date;
    }

    public async Task<bool> HasActivityForTimeGranularity(
        int tagId,
        DateTime dateStarted,
        Guid userId,
        int? excludeActivityId = null
    )
    {
        var tag = await _context.Tags.FindAsync(tagId);
        if (tag == null || tag.TimeGranularity == TimeGranularity.Exact)
        {
            return false; // No validation for Exact granularity at the moment
        }

        DateTime startRange,
            endRange;

        // Determine the date range based on time granularity
        switch (tag.TimeGranularity)
        {
            case TimeGranularity.Daily:
                startRange = dateStarted.Date;
                endRange = startRange.AddDays(1).AddTicks(-1);
                break;
            case TimeGranularity.Hourly:
                startRange = new DateTime(
                    dateStarted.Year,
                    dateStarted.Month,
                    dateStarted.Day,
                    dateStarted.Hour,
                    0,
                    0
                );
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

        // Use specification to check for duplicate
        var spec = new DuplicateActivityCheckSpec(tagId, userId, startRange, endRange, excludeActivityId);

        return await _activityRepository.AnyAsync(spec);
    }

    public async Task<List<ActivityResponse>> GetByYear(int year, Guid userId, int? tagId = null)
    {
        var spec = new ActivitiesForYearSpec(year, userId, tagId);
        var activities = await _activityRepository.GetAsync(spec);

        return [.. activities
            .Select(a => new ActivityResponse
            {
                Id = a.Id,
                DateStarted = a.DateStarted,
                DateFinished = a.DateFinished,
                DateCreated = a.DateCreated,
                Description = a.Description,
                PrimaryTagId = a.TagId,
                PrimaryTagName = a.Tag?.Group?.Name != null ? $"{a.Tag.Group.Name}: {a.Tag.TagName}" : a.Tag?.TagName ?? string.Empty,
                PrimaryTagValue = a.Description ?? string.Empty,
                ElementId = a.Tag?.InputType?.Id,
                ElementName = a.Tag?.InputType?.Name ?? string.Empty,
                TagRequired = a.Tag?.IsRequired ?? false,
            })];
    }

    public async Task<List<int>> GetAvailableYears(Guid userId, int? tagId = null)
    {
        return await _activityRepository.GetAvailableYearsAsync(userId, tagId);
    }

    public async Task<List<TagResponse>> GetRequiredDailyTagsNotFilledForDate(DateTime date, Guid userId)
    {
        var unfilledTags = await _activityRepository.GetRequiredDailyTagsNotFilledAsync(date, userId);

        return [.. unfilledTags.Select(tag => new TagResponse
        {
            Id = tag.Id,
            Title = tag.Group?.Name != null ? $"{tag.Group.Name}: {tag.TagName}" : tag.TagName,
            InputTypeId = tag.InputTypeId,
            TypeId = tag.InputTypeId,
            IsRequired = tag.IsRequired,
            IsRepeatable = tag.IsRepeatable,
            TimeGranularity = tag.TimeGranularity,
            IsRange = tag.IsRange,
            UnitId = tag.UnitId,
            UnitSymbol = tag.Unit != null ? tag.Unit.Symbol : null,
            MinValue = tag.MinValue,
            MaxValue = tag.MaxValue,
            Step = tag.Step,
            DefaultValue = tag.DefaultValue,
            OptionListId = tag.OptionListId,
            OptionListName = tag.OptionList != null ? tag.OptionList.Name : null,
        })];
    }

    // Private helper methods for refactored pagination

    /// <summary>
    /// Applies common filters (tag, date range, description) to an activity query.
    /// </summary>
    private IQueryable<Activity> ApplyActivityFilters(
        IQueryable<Activity> query,
        int? tagId,
        DateTime? startDate,
        DateTime? endDate,
        string? descriptionFilter)
    {
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
            query = query.Where(a =>
                a.Description != null && a.Description.Contains(descriptionFilter)
            );
        }

        return query;
    }

    /// <summary>
    /// Generic helper for paginating activities by time period (week/month).
    /// </summary>
    private async Task<PagedResult<ActivityResponse>> GetPagedByTimePeriod(
        IQueryable<Activity> baseQuery,
        int pageNumber,
        int periodsPerPage,
        string orderBy,
        Func<DateTime, int?> periodOffsetCalculator,
        string periodType)
    {
        // Group by time period using the provided calculator
        var periodGroupsQuery = baseQuery
            .GroupBy(a => periodOffsetCalculator(a.DateStarted))
            .Select(g => new
            {
                PeriodOffset = g.Key,
                Count = g.Count(),
                MinDate = g.Min(a => a.DateStarted)
            });

        // Apply ordering at SQL level
        var isDescending = orderBy?.ToLower() == "desc";
        periodGroupsQuery = isDescending
            ? periodGroupsQuery.OrderByDescending(g => g.PeriodOffset)
            : periodGroupsQuery.OrderBy(g => g.PeriodOffset);

        // Get total period count
        var totalPeriods = await periodGroupsQuery.CountAsync();

        if (totalPeriods == 0)
        {
            return CreateEmptyPagedResult(pageNumber, periodsPerPage);
        }

        // Get the period offsets for the current page (pagination in SQL)
        var pagedPeriodOffsets = await periodGroupsQuery
            .Skip((pageNumber - 1) * periodsPerPage)
            .Take(periodsPerPage)
            .Select(g => g.PeriodOffset)
            .ToListAsync();

        // Fetch only activities that belong to these periods
        var activitiesInSelectedPeriods = await baseQuery.Where(a => pagedPeriodOffsets.Contains(periodOffsetCalculator(a.DateStarted))).ToListAsync();

        // Order activities within the selected periods
        activitiesInSelectedPeriods = SortActivities(activitiesInSelectedPeriods, orderBy);

        return new PagedResult<ActivityResponse>
        {
            Items = activitiesInSelectedPeriods.Select(MapToResponse).ToList(),
            TotalCount = totalPeriods,
            PageNumber = pageNumber,
            PageSize = periodsPerPage,
        };
    }

    /// <summary>
    /// Sorts activities by DateStarted based on order direction.
    /// </summary>
    private List<Activity> SortActivities(List<Activity> activities, string? orderBy)
    {
        return orderBy?.ToLower() == "asc"
            ? activities.OrderBy(a => a.DateStarted).ToList()
            : activities.OrderByDescending(a => a.DateStarted).ToList();
    }

    /// <summary>
    /// Creates an empty paged result.
    /// </summary>
    private PagedResult<ActivityResponse> CreateEmptyPagedResult(int pageNumber, int pageSize)
    {
        return new PagedResult<ActivityResponse>
        {
            Items = new List<ActivityResponse>(),
            TotalCount = 0,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
    }
}
