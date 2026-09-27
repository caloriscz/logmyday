using LogMyDay.Api.Application.Interfaces;
using System.Globalization;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Infrastructure.Repositories;
using LogMyDay.Api.Infrastructure.Specifications;
using LogMyDay.Domain.Entities;
using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LogMyDay.Api.Application.Services;

public class ActivityService : IActivityService
{
    private readonly LogMyDayDbContext _context;
    private readonly IActivityRepository _activityRepository;
    private readonly IEventLogService _eventLogService;
    private readonly ITagDayLockService _tagDayLockService;
    private readonly ITagRuleEngine _tagRuleEngine;

    public ActivityService(
        LogMyDayDbContext context,
        IActivityRepository activityRepository,
        IEventLogService eventLogService,
        ITagDayLockService tagDayLockService,
        ITagRuleEngine? tagRuleEngine = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _activityRepository = activityRepository ?? throw new ArgumentNullException(nameof(activityRepository));
        _eventLogService = eventLogService ?? throw new ArgumentNullException(nameof(eventLogService));
        _tagDayLockService = tagDayLockService ?? throw new ArgumentNullException(nameof(tagDayLockService));
        _tagRuleEngine = tagRuleEngine ?? new TagRuleEngine(context);
    }

    private static async Task CommitIfOwned(IDbContextTransaction? transaction)
    {
        if (transaction != null)
        {
            await transaction.CommitAsync();
        }
    }

    // A source write and the rule results it changes are saved together. The in-memory test
    // provider has no transactions, and an outer transaction (e.g. backup) is joined, not nested.
    private async Task<IDbContextTransaction?> BeginWriteTransaction()
    {
        return _context.Database.IsRelational() && _context.Database.CurrentTransaction == null
            ? await _context.Database.BeginTransactionAsync()
            : null;
    }

    public async Task<ActivityResponse> Create(ActivityRequest calendarRequest, Guid userId, bool isSkipMarker = false)
    {
        // Get the tag to check if it's repeatable and what its time granularity is
        var tag = await FindOwnedTag(calendarRequest.PrimaryTagId, userId) ?? throw new KeyNotFoundException("Tag not found");
        if (tag.IsComputed)
        {
            throw new TagComputedException(tag.Id);
        }

        await using var transaction = await BeginWriteTransaction();

        // TagDayLock enforcement: reject any activity for a locked (UserId, TagId, localDate)
        // triple. The client (web/mobile) catches the 409 and offers an unlock-and-retry prompt.
        calendarRequest.DateStarted = await ToUserLocalTime(userId, calendarRequest.DateStarted);
        calendarRequest.DateFinished = calendarRequest.DateFinished.HasValue
            ? await ToUserLocalTime(userId, calendarRequest.DateFinished.Value)
            : null;
        var activityLocalDate = DateOnly.FromDateTime(calendarRequest.DateStarted);
        var existingLock = await _tagDayLockService.Find(userId, tag.Id, activityLocalDate);
        if (existingLock?.IsLocked == true)
        {
            throw new TagDayLockedException(tag.Id, activityLocalDate);
        }

        var isNumeric = tag.InputTypeId is 1 or 6; // Integer or Decimal
        var hasNumber = ActivityValueCodec.TryReadNumber(calendarRequest.Description, out var number);

        // Non-repeatable tag handling
        if (!tag.IsRepeatable && tag.TimeGranularity != TimeGranularity.Exact)
        {
            if (isNumeric)
            {
                // Numeric non-repeatable: accumulate by Step (update existing row). The earliest
                // row of the period is the one that accumulates, so the choice is stable.
                var (existingActivities, dateRange) = await GetActivitiesInPeriod(tag.Id, calendarRequest.DateStarted, userId);
                var existing = existingActivities
                    .OrderBy(a => a.DateStarted)
                    .ThenBy(a => a.Id)
                    .FirstOrDefault();

                if (existing != null)
                {
                    // Increment by the value the request carries (e.g. a reminder's "Add 20 mg").
                    // Fall back to the tag's Step only when the request has no numeric value,
                    // preserving the quick-tap "+Step" behavior.
                    var increment = hasNumber ? number : tag.Step ?? 1.0;
                    var currentValue = ActivityValueCodec.TryReadNumber(existing.Description, out var parsed) ? parsed : 0.0;
                    var newValue = currentValue + increment;

                    if (tag.MaxValue.HasValue && newValue > tag.MaxValue.Value)
                    {
                        throw new InvalidOperationException(
                            $"Adding {increment} would bring this tag to {newValue}, which exceeds the maximum {tag.MaxValue.Value} (current value {currentValue})."
                        );
                    }

                    EnsureWithinLimits(tag, newValue, isSkipMarker);

                    existing.Description = ActivityValueCodec.WriteNumber(tag.InputTypeId, newValue);

                    await _activityRepository.UpdateAsync(existing);
                    await _activityRepository.SaveChangesAsync();

                    await _context.Entry(existing).Reference(a => a.Tag).LoadAsync();
                    if (existing.Tag is not null)
                    {
                        await _context.Entry(existing.Tag).Reference(t => t.InputType).LoadAsync();
                        await _context.Entry(existing.Tag).Reference(t => t.Group).LoadAsync();
                    }

                    await _eventLogService.Log(userId, EventLogLevel.Info,
                        $"Activity '{tag.TagName}' increased by {ActivityValueCodec.WriteNumber(tag.InputTypeId, increment)} to {existing.Description}");

                    await _tagRuleEngine.OnSourcesChanged(userId, [(tag.Id, DateOnly.FromDateTime(existing.DateStarted))]);
                    await CommitIfOwned(transaction);

                    return MapToResponse(existing);
                }
            }
            else
            {
                // Non-numeric non-repeatable: keep current blocking behavior
                if (await HasActivityForTimeGranularity(tag.Id, calendarRequest.DateStarted, userId))
                {
                    throw new InvalidOperationException(
                        $"An activity for this tag already exists for the selected {tag.TimeGranularity.ToString().ToLower()} period. This tag is not repeatable."
                    );
                }
            }
        }

        if (isNumeric && hasNumber)
        {
            EnsureWithinLimits(tag, number, isSkipMarker);
        }

        // Repeatable + MaxValue enforcement for numeric tags
        if (tag.IsRepeatable && isNumeric && tag.MaxValue.HasValue && tag.TimeGranularity != TimeGranularity.Exact)
        {
            var (existingActivities, _) = await GetActivitiesInPeriod(tag.Id, calendarRequest.DateStarted, userId);
            var currentSum = existingActivities
                .Sum(a => ActivityValueCodec.TryReadNumber(a.Description, out var v) ? v : 0.0);
            var newValue = hasNumber ? number : 0.0;

            if (currentSum + newValue > tag.MaxValue.Value)
            {
                throw new InvalidOperationException(
                    $"Adding {newValue} would bring the daily total to {currentSum + newValue}, which exceeds the maximum {tag.MaxValue.Value} (current total {currentSum})."
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

        var tagName = reloadedActivity.Tag?.TagName ?? "unknown";
        var desc = string.IsNullOrWhiteSpace(reloadedActivity.Description)
            ? string.Empty
            : $" of value {reloadedActivity.Description}";
        await _eventLogService.Log(userId, EventLogLevel.Info, $"Activity '{tagName}'{desc} added");

        await _tagRuleEngine.OnSourcesChanged(userId, [(tag.Id, DateOnly.FromDateTime(activity.DateStarted))]);
        await CommitIfOwned(transaction);

        // TagDayLock is intentionally user-driven only — no auto-lock here. Non-repeatable
        // numeric tags accumulate via Step up to MaxValue across multiple activity creates;
        // an auto-lock after the first one breaks that flow (see 2026-05-29 task). The lock
        // is a deliberate "I'm done with this tag for today" gesture from the user.

        return MapToResponse(reloadedActivity);
    }

    /// <summary>Activity times are stored as naive local time in the user's time zone. An
    /// unspecified value is already that and is kept as is. A value that carries a kind (UTC, or
    /// Local after the JSON binder applied an offset in the server's zone) is converted into the
    /// user's time zone. Falls back to UTC if the user has no valid TimeZone.</summary>
    private async Task<DateTime> ToUserLocalTime(Guid userId, DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
        {
            return value;
        }

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(user?.TimeZone ?? "UTC");
        }
        catch (TimeZoneNotFoundException)
        {
            tz = TimeZoneInfo.Utc;
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(value.ToUniversalTime(), tz);

        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }

    public async Task<bool> Delete(int id, Guid userId)
    {
        var spec = new ActivityByIdAndUserSpec(id, userId);
        var activity = await _activityRepository.GetSingleAsync(spec);

        if (activity == null)
        {
            return false;
        }

        if (activity.RuleId != null || activity.Tag?.IsComputed == true)
        {
            throw new TagComputedException(activity.TagId);
        }

        await using var transaction = await BeginWriteTransaction();

        await _activityRepository.DeleteAsync(activity);
        await _activityRepository.SaveChangesAsync();

        var desc = string.IsNullOrWhiteSpace(activity.Description) ? string.Empty : $" of value {activity.Description}";
        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Activity '{activity.Tag?.TagName ?? "unknown"}'{desc} on {activity.DateStarted:yyyy-MM-dd} deleted");

        await _tagRuleEngine.OnSourcesChanged(userId, [(activity.TagId, DateOnly.FromDateTime(activity.DateStarted))]);
        await CommitIfOwned(transaction);

        return true;
    }

    public async Task<ActivityResponse> ReplaceForDay(ActivityRequest request, Guid userId)
    {
        if (request.PrimaryTagId is not int tagId)
        {
            throw new ArgumentException("A tag is required");
        }

        request.DateStarted = await ToUserLocalTime(userId, request.DateStarted);
        var dayStart = request.DateStarted.Date;
        var dayEnd = dayStart.AddDays(1);

        var existing = await _context.Activities
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.TagId == tagId && a.DateStarted >= dayStart && a.DateStarted < dayEnd)
            .OrderBy(a => a.DateStarted)
            .ThenBy(a => a.Id)
            .Select(a => (int?)a.Id)
            .FirstOrDefaultAsync();

        return existing is int existingId
            ? await Update(existingId, request, userId)
            : await Create(request, userId);
    }

    // A single stored value must lie within the tag's MinValue/MaxValue. A reminder skip's zero row
    // is exempt from MinValue: it marks the day as skipped rather than carrying a measured value.
    private static void EnsureWithinLimits(Tag tag, double value, bool isSkipMarker)
    {
        if (!isSkipMarker && tag.MinValue.HasValue && value < tag.MinValue.Value)
        {
            throw new InvalidOperationException(
                $"Value {value} is below the minimum {tag.MinValue.Value} allowed for this tag.");
        }

        if (tag.MaxValue.HasValue && value > tag.MaxValue.Value)
        {
            throw new InvalidOperationException(
                $"Value {value} exceeds the maximum {tag.MaxValue.Value} allowed for this tag.");
        }
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
        var rawQuery = _context.Activities.Where(a => a.UserId == userId).AsQueryable();
        rawQuery = ApplyActivityFilters(rawQuery, tagId, startDate, endDate, descriptionFilter);

        // Step 1: get distinct week offsets — EF-translatable, no includes needed
        var allOffsets = await rawQuery
            .Select(a => a.DateStarted.Year * 1000 + a.DateStarted.DayOfYear / 7)
            .Distinct()
            .ToListAsync();

        if (allOffsets.Count == 0)
        {
            return CreateEmptyPagedResult(weekPageNumber, weeksPerPage);
        }

        // Always paginate newest-first so page 1 is always the most recent week.
        // The orderBy parameter only affects sorting of activities within the period.
        var orderedOffsets = allOffsets.OrderByDescending(o => o).ToList();

        var totalPeriods = orderedOffsets.Count;
        var pagedOffsets = orderedOffsets
            .Skip((weekPageNumber - 1) * weeksPerPage)
            .Take(weeksPerPage)
            .ToList();

        // Step 2: fetch activities only for the selected weeks, with all includes
        var fullQuery = _context.Activities
            .Include(ct => ct.Tag).ThenInclude(t => t.InputType)
            .Include(ct => ct.Tag).ThenInclude(t => t.Group)
            .Where(a => a.UserId == userId)
            .AsQueryable();
        fullQuery = ApplyActivityFilters(fullQuery, tagId, startDate, endDate, descriptionFilter);

        var activities = await fullQuery
            .Where(a => pagedOffsets.Contains(a.DateStarted.Year * 1000 + a.DateStarted.DayOfYear / 7))
            .ToListAsync();

        activities = SortActivities(activities, orderBy);

        return new PagedResult<ActivityResponse>
        {
            Items = activities.Select(MapToResponse).ToList(),
            TotalCount = totalPeriods,
            PageNumber = weekPageNumber,
            PageSize = weeksPerPage,
        };
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
        var rawQuery = _context.Activities.Where(a => a.UserId == userId).AsQueryable();
        rawQuery = ApplyActivityFilters(rawQuery, tagId, startDate, endDate, descriptionFilter);

        // Step 1: get distinct month offsets — EF-translatable, no includes needed
        var allOffsets = await rawQuery
            .Select(a => a.DateStarted.Year * 12 + a.DateStarted.Month)
            .Distinct()
            .ToListAsync();

        if (allOffsets.Count == 0)
        {
            return CreateEmptyPagedResult(monthPageNumber, monthsPerPage);
        }

        // Always paginate newest-first so page 1 is always the most recent month.
        // The orderBy parameter only affects sorting of activities within the period.
        var orderedOffsets = allOffsets.OrderByDescending(o => o).ToList();

        var totalPeriods = orderedOffsets.Count;
        var pagedOffsets = orderedOffsets
            .Skip((monthPageNumber - 1) * monthsPerPage)
            .Take(monthsPerPage)
            .ToList();

        // Step 2: fetch activities only for the selected months, with all includes
        var fullQuery = _context.Activities
            .Include(ct => ct.Tag).ThenInclude(t => t.InputType)
            .Include(ct => ct.Tag).ThenInclude(t => t.Group)
            .Where(a => a.UserId == userId)
            .AsQueryable();
        fullQuery = ApplyActivityFilters(fullQuery, tagId, startDate, endDate, descriptionFilter);

        var activities = await fullQuery
            .Where(a => pagedOffsets.Contains(a.DateStarted.Year * 12 + a.DateStarted.Month))
            .ToListAsync();

        activities = SortActivities(activities, orderBy);

        return new PagedResult<ActivityResponse>
        {
            Items = activities.Select(MapToResponse).ToList(),
            TotalCount = totalPeriods,
            PageNumber = monthPageNumber,
            PageSize = monthsPerPage,
        };
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
        var tag = await FindOwnedTag(tagId, userId) ?? throw new KeyNotFoundException("Tag not found");

        if (activity.RuleId != null || activity.Tag?.IsComputed == true)
        {
            throw new TagComputedException(activity.TagId);
        }

        if (tag.IsComputed)
        {
            throw new TagComputedException(tag.Id);
        }

        request.DateStarted = await ToUserLocalTime(userId, request.DateStarted);
        request.DateFinished = request.DateFinished.HasValue
            ? await ToUserLocalTime(userId, request.DateFinished.Value)
            : null;

        // The window the row leaves, for rules that use the old tag or day as a source.
        var previous = (activity.TagId, DateOnly.FromDateTime(activity.DateStarted));

        if (!tag.IsRepeatable && tag.TimeGranularity != TimeGranularity.Exact)
        {
            if (await HasActivityForTimeGranularity(tag.Id, request.DateStarted, userId, excludeActivityId: id))
            {
                throw new InvalidOperationException(
                    $"An activity for this tag already exists for the selected {tag.TimeGranularity.ToString().ToLower()} period. This tag is not repeatable."
                );
            }
        }

        // The same value limits as Create. For a repeatable tag the period total is the other
        // rows of the period plus the edited value. TagDayLock intentionally does not apply to
        // edits: the lock only stops new entries.
        if (tag.InputTypeId is 1 or 6 && ActivityValueCodec.TryReadNumber(request.Description, out var number))
        {
            EnsureWithinLimits(tag, number, isSkipMarker: false);

            if (tag.IsRepeatable && tag.MaxValue.HasValue && tag.TimeGranularity != TimeGranularity.Exact)
            {
                var (others, _) = await GetActivitiesInPeriod(tag.Id, request.DateStarted, userId, excludeActivityId: id);
                var otherSum = others.Sum(a => ActivityValueCodec.TryReadNumber(a.Description, out var v) ? v : 0.0);

                if (otherSum + number > tag.MaxValue.Value)
                {
                    throw new InvalidOperationException(
                        $"A value of {number} would bring the total to {otherSum + number}, which exceeds the maximum {tag.MaxValue.Value} (other entries total {otherSum}).");
                }
            }
        }

        await using var transaction = await BeginWriteTransaction();

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

        var desc = string.IsNullOrWhiteSpace(activity.Description) ? string.Empty : $" to value {activity.Description}";
        await _eventLogService.Log(userId, EventLogLevel.Info,
            $"Activity '{tag.TagName}' on {activity.DateStarted:yyyy-MM-dd} updated{desc}");

        await _tagRuleEngine.OnSourcesChanged(userId, [previous, (tag.Id, DateOnly.FromDateTime(activity.DateStarted))]);
        await CommitIfOwned(transaction);

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
            RuleId = calendar.RuleId,
            IsGenerated = calendar.RuleId != null,
        };
    }

    // Tags are strictly per user: a tag id owned by another user resolves to null, same as a missing one.
    private async Task<Tag?> FindOwnedTag(int? tagId, Guid userId)
    {
        if (tagId is null)
        {
            return null;
        }

        return await _context.Tags.FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId);
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
        var tag = await FindOwnedTag(tagId, userId);
        if (tag == null || tag.TimeGranularity == TimeGranularity.Exact)
        {
            return false;
        }

        var (startRange, endRange) = GetDateRangeForGranularity(tag.TimeGranularity, dateStarted);
        var spec = new DuplicateActivityCheckSpec(tagId, userId, startRange, endRange, excludeActivityId);

        return await _activityRepository.AnyAsync(spec);
    }

    public async Task<PeriodSumResponse> GetPeriodSum(int tagId, DateTime dateStarted, Guid userId, int? excludeActivityId = null)
    {
        var tag = await FindOwnedTag(tagId, userId) ?? throw new KeyNotFoundException("Tag not found");

        var isNumeric = tag.InputTypeId is 1 or 6;
        var response = new PeriodSumResponse
        {
            MaxValue = tag.MaxValue,
            IsNonRepeatableNumeric = !tag.IsRepeatable && isNumeric,
        };

        if (tag.TimeGranularity == TimeGranularity.Exact)
        {
            return response;
        }

        var (activities, _) = await GetActivitiesInPeriod(tagId, dateStarted, userId, excludeActivityId);
        response.CurrentSum = activities.Sum(a => ActivityValueCodec.TryReadNumber(a.Description, out var v) ? v : 0.0);
        response.RemainingCapacity = tag.MaxValue.HasValue ? tag.MaxValue.Value - response.CurrentSum : null;

        if (!tag.IsRepeatable && isNumeric)
        {
            var existing = activities.OrderBy(a => a.DateStarted).ThenBy(a => a.Id).FirstOrDefault();
            if (existing != null)
            {
                response.ExistingValue = ActivityValueCodec.TryReadNumber(existing.Description, out var ev) ? ev : 0.0;
                response.ExistingActivityId = existing.Id;
            }
        }

        return response;
    }

    private async Task<(List<Activity> activities, (DateTime start, DateTime end) dateRange)> GetActivitiesInPeriod(
        int tagId,
        DateTime dateStarted,
        Guid userId,
        int? excludeActivityId = null)
    {
        var tag = await FindOwnedTag(tagId, userId);
        if (tag == null || tag.TimeGranularity == TimeGranularity.Exact)
        {
            return (new List<Activity>(), (dateStarted, dateStarted));
        }

        var (startRange, endRange) = GetDateRangeForGranularity(tag.TimeGranularity, dateStarted);
        var spec = new DuplicateActivityCheckSpec(tagId, userId, startRange, endRange, excludeActivityId);
        var activities = await _activityRepository.GetAsync(spec);

        return (activities, (startRange, endRange));
    }

    private (DateTime startRange, DateTime endRange) GetDateRangeForGranularity(TimeGranularity granularity, DateTime dateStarted)
    {
        return granularity switch
        {
            TimeGranularity.Daily => (dateStarted.Date, dateStarted.Date.AddDays(1).AddTicks(-1)),
            TimeGranularity.Hourly => (
                new DateTime(dateStarted.Year, dateStarted.Month, dateStarted.Day, dateStarted.Hour, 0, 0),
                new DateTime(dateStarted.Year, dateStarted.Month, dateStarted.Day, dateStarted.Hour, 0, 0).AddHours(1).AddTicks(-1)
            ),
            TimeGranularity.Weekly => (GetStartOfWeek(dateStarted), GetStartOfWeek(dateStarted).AddDays(7).AddTicks(-1)),
            TimeGranularity.Monthly => (
                new DateTime(dateStarted.Year, dateStarted.Month, 1),
                new DateTime(dateStarted.Year, dateStarted.Month, 1).AddMonths(1).AddTicks(-1)
            ),
            TimeGranularity.Yearly => (
                new DateTime(dateStarted.Year, 1, 1),
                new DateTime(dateStarted.Year, 1, 1).AddYears(1).AddTicks(-1)
            ),
            _ => (dateStarted, dateStarted),
        };
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
                RuleId = a.RuleId,
                IsGenerated = a.RuleId != null,
            })];
    }

    public async Task<List<int>> GetAvailableYears(Guid userId, int? tagId = null)
    {
        return await _activityRepository.GetAvailableYearsAsync(userId, tagId);
    }

    public async Task<bool> HasActivityForTagOnDate(int tagId, DateOnly date, Guid userId)
    {
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = date.AddDays(1).ToDateTime(TimeOnly.MinValue).AddTicks(-1);
        var spec = new DuplicateActivityCheckSpec(tagId, userId, start, end);

        return await _activityRepository.AnyAsync(spec);
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
    /// Sorts activities by DateStarted based on order direction.
    /// </summary>
    private List<Activity> SortActivities(List<Activity> activities, string? orderBy)
    {
        return orderBy?.ToLower() switch
        {
            "asc" => activities.OrderBy(a => a.DateStarted).ToList(),
            "group-asc" => activities
                .OrderBy(a => a.Tag?.Group?.Name ?? "")
                .ThenBy(a => a.Tag?.TagName ?? "")
                .ThenBy(a => a.DateStarted)
                .ToList(),
            "group-desc" => activities
                .OrderByDescending(a => a.Tag?.Group?.Name ?? "")
                .ThenByDescending(a => a.Tag?.TagName ?? "")
                .ThenByDescending(a => a.DateStarted)
                .ToList(),
            _ => activities.OrderByDescending(a => a.DateStarted).ToList(),
        };
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
