using LogMyDay.Domain.Entities;

namespace LogMyDay.Api.Infrastructure.Specifications;

/// <summary>
/// Specification for activities owned by a specific user with tag and input type includes.
/// </summary>
public class ActivitiesForUserSpec : BaseSpecification<Activity>
{
    public ActivitiesForUserSpec(Guid userId) 
        : base(a => a.UserId == userId)
    {
        AddInclude(a => a.Tag);
        AddInclude("Tag.InputType");
        AddInclude("Tag.Group");
    }
}

/// <summary>
/// Specification for a single activity by ID and user (for GetById, Update, Delete operations).
/// </summary>
public class ActivityByIdAndUserSpec : BaseSpecification<Activity>
{
    public ActivityByIdAndUserSpec(int activityId, Guid userId)
        : base(a => a.Id == activityId && a.UserId == userId)
    {
        AddInclude(a => a.Tag);
        AddInclude("Tag.InputType");
        AddInclude("Tag.Group");
    }
}

/// <summary>
/// Specification for activities within a specific date range.
/// </summary>
public class ActivitiesWithinDateRangeSpec : BaseSpecification<Activity>
{
    public ActivitiesWithinDateRangeSpec(DateTime startDate, DateTime endDate)
        : base(a => a.DateStarted >= startDate && a.DateStarted <= endDate)
    {
    }

    public ActivitiesWithinDateRangeSpec(DateTime startDate)
        : base(a => a.DateStarted >= startDate)
    {
    }
}

/// <summary>
/// Specification for activities with a specific tag.
/// </summary>
public class ActivitiesWithTagSpec : BaseSpecification<Activity>
{
    public ActivitiesWithTagSpec(int tagId)
        : base(a => a.TagId == tagId)
    {
    }
}

/// <summary>
/// Specification for activities matching a description filter.
/// </summary>
public class ActivitiesWithDescriptionSpec : BaseSpecification<Activity>
{
    public ActivitiesWithDescriptionSpec(string descriptionFilter)
        : base(a => a.Description != null && a.Description.Contains(descriptionFilter))
    {
    }
}

/// <summary>
/// Composite specification for paged activities with all common filters.
/// Combines user scoping, optional filters, ordering, and pagination.
/// </summary>
public class PagedActivitiesSpec : BaseSpecification<Activity>
{
    public PagedActivitiesSpec(
        Guid userId,
        int pageNumber,
        int pageSize,
        string orderBy = "desc",
        int? tagId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? descriptionFilter = null)
        : base(a => a.UserId == userId)
    {
        // Add includes for related entities
        AddInclude(a => a.Tag);
        AddInclude("Tag.InputType");
        AddInclude("Tag.Group");

        // Apply optional filters
        if (tagId.HasValue)
        {
            AddCriteria(a => a.TagId == tagId.Value);
        }

        if (startDate.HasValue)
        {
            AddCriteria(a => a.DateStarted >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            AddCriteria(a => a.DateStarted <= endDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(descriptionFilter))
        {
            AddCriteria(a => a.Description != null && a.Description.Contains(descriptionFilter));
        }

        // Apply ordering
        switch (orderBy?.ToLower())
        {
            case "asc":
                ApplyOrderBy(a => a.DateStarted);
                break;
            case "group-asc":
                ApplyOrderBy(a => a.Tag.Group != null ? a.Tag.Group.Name : "");
                ApplyThenOrderBy(a => a.Tag.TagName.ToLower());
                break;
            case "group-desc":
                ApplyOrderByDescending(a => a.Tag.Group != null ? a.Tag.Group.Name : "");
                ApplyThenOrderBy(a => a.Tag.TagName.ToLower());
                break;
            default:
                ApplyOrderByDescending(a => a.DateStarted);
                break;
        }

        // Apply pagination
        ApplyPaging((pageNumber - 1) * pageSize, pageSize);
    }
}

/// <summary>
/// Specification for checking duplicate activities for non-repeatable tags.
/// </summary>
public class DuplicateActivityCheckSpec : BaseSpecification<Activity>
{
    public DuplicateActivityCheckSpec(
        int tagId,
        Guid userId,
        DateTime startRange,
        DateTime endRange,
        int? excludeActivityId = null)
        : base(a => 
            a.TagId == tagId &&
            a.UserId == userId &&
            a.DateStarted >= startRange &&
            a.DateStarted <= endRange)
    {
        if (excludeActivityId.HasValue)
        {
            AddCriteria(a => a.Id != excludeActivityId.Value);
        }
    }
}

/// <summary>
/// Specification for activities for a specific year with optional tag filter.
/// </summary>
public class ActivitiesForYearSpec : BaseSpecification<Activity>
{
    public ActivitiesForYearSpec(int year, Guid userId, int? tagId = null)
        : base(a => 
            a.UserId == userId &&
            a.DateStarted >= new DateTime(year, 1, 1) &&
            a.DateStarted < new DateTime(year + 1, 1, 1))
    {
        AddInclude(a => a.Tag);
        AddInclude("Tag.InputType");
        AddInclude("Tag.Group");

        if (tagId.HasValue)
        {
            AddCriteria(a => a.TagId == tagId.Value);
        }

        ApplyOrderBy(a => a.DateStarted);
    }
}

/// <summary>
/// Specification for required tags that haven't been filled for a specific date.
/// Returns all required tags regardless of time granularity.
/// </summary>
public class RequiredDailyTagsNotFilledSpec : BaseSpecification<Tag>
{
    public RequiredDailyTagsNotFilledSpec(DateTime date, Guid userId)
        : base(t => 
            t.UserId == userId &&
            t.IsRequired)
    {
        AddInclude(t => t.InputType);
        AddInclude(t => t.Unit);
        AddInclude(t => t.OptionList);
    }
}

/// <summary>
/// Specification for activities by a specific user and tag for a date range.
/// Used to check if required tags have been filled.
/// </summary>
public class ActivitiesForUserTagAndDateSpec : BaseSpecification<Activity>
{
    public ActivitiesForUserTagAndDateSpec(Guid userId, int tagId, DateTime startOfDay, DateTime endOfDay)
        : base(a =>
            a.UserId == userId &&
            a.TagId == tagId &&
            a.DateStarted >= startOfDay &&
            a.DateStarted <= endOfDay)
    {
    }
}
