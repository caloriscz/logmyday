using System.ComponentModel;
using LogMyDay.Api.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services.Ai;

/// <summary>
/// AI tool functions for metadata queries. These functions return read-only metadata only - no personal activity data.
/// </summary>
public sealed class AiToolFunctions
{
    private readonly ITagService _tagService;
    private readonly IActivityService _activityService;
    private readonly IUnitService _unitService;
    private readonly ITagOptionListService _optionListService;
    private readonly ILogger<AiToolFunctions> _logger;

    public AiToolFunctions(
        ITagService tagService,
        IActivityService activityService,
        IUnitService unitService,
        ITagOptionListService optionListService,
        ILogger<AiToolFunctions> logger)
    {
        _tagService = tagService;
        _activityService = activityService;
        _unitService = unitService;
        _optionListService = optionListService;
        _logger = logger;
    }

    [Description("Get a list of all tags for the current user, including their properties like input type, whether they are required, and time granularity.")]
    public async Task<object> GetTags(
        [Description("The user ID to fetch tags for")] Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tags = await _tagService.GetAll(userId);

            return tags.Select(t => new
            {
                Name = t.Title,
                InputType = GetInputTypeName(t.InputTypeId ?? 0),
                IsRequired = t.IsRequired,
                TimeGranularity = t.TimeGranularity.ToString(),
                HasUnit = t.UnitId.HasValue,
                HasOptionList = t.OptionListId.HasValue,
                IsRepeatable = t.IsRepeatable,
                IsRange = t.IsRange
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching tags for user {UserId}", userId);

            return new { Error = "Failed to fetch tags" };
        }
    }

    [Description("Get aggregated statistics about the user's activities and tags, such as total counts and date ranges.")]
    public async Task<object> GetStatistics(
        [Description("The user ID to fetch statistics for")] Guid userId,
        [Description("Optional tag ID to get statistics for a specific tag")] int? tagId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tags = await _tagService.GetAll(userId);
            var activities = await _activityService.GetPaged(1, 10000, "DateStarted", userId, tagId);

            var filteredActivities = tagId.HasValue
                ? activities.Items.Where(a => a.PrimaryTagId == tagId.Value).ToList()
                : activities.Items.ToList();

            var oldestActivity = filteredActivities.MinBy(a => a.DateStarted);
            var newestActivity = filteredActivities.MaxBy(a => a.DateStarted);

            return new
            {
                TotalActivities = filteredActivities.Count,
                TotalTags = tags.Count,
                OldestActivityDate = oldestActivity?.DateStarted.ToString("yyyy-MM-dd"),
                NewestActivityDate = newestActivity?.DateStarted.ToString("yyyy-MM-dd"),
                TagName = tagId.HasValue ? tags.FirstOrDefault(t => t.Id == tagId.Value)?.Title : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching statistics for user {UserId}", userId);

            return new { Error = "Failed to fetch statistics" };
        }
    }

    [Description("Get a list of available chart types that can be used to visualize numeric tag data.")]
    public Task<object> GetChartTypes(CancellationToken cancellationToken = default)
    {
        var chartTypes = new[]
        {
            new { Name = "Line", Description = "Line chart showing data points connected over time" },
            new { Name = "Area", Description = "Area chart with filled region under the line" },
            new { Name = "Bar", Description = "Bar chart showing values as vertical bars" }
        };

        return Task.FromResult<object>(chartTypes);
    }

    [Description("Get a list of measurement units available for numeric tags.")]
    public async Task<object> GetUnits(
        [Description("The user ID to fetch units for")] Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var units = await _unitService.GetAll();

            return units.Select(u => new
            {
                Name = u.Key,
                Symbol = u.Symbol
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching units for user {UserId}", userId);

            return new { Error = "Failed to fetch units" };
        }
    }

    [Description("Get a list of option lists (predefined value lists) available for tags.")]
    public async Task<object> GetOptionLists(
        [Description("The user ID to fetch option lists for")] Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var optionLists = await _optionListService.GetAll(userId);

            return optionLists.Select(ol => new
            {
                Name = ol.Name,
                ItemCount = ol.Options?.Count ?? 0
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching option lists for user {UserId}", userId);

            return new { Error = "Failed to fetch option lists" };
        }
    }

    [Description("Get a list of available input types for tags, such as Integer, String, Boolean, Date, etc.")]
    public Task<object> GetInputTypes(CancellationToken cancellationToken = default)
    {
        var inputTypes = new[]
        {
            new { Id = 1, Name = "Integer" },
            new { Id = 2, Name = "String" },
            new { Id = 3, Name = "Boolean" },
            new { Id = 4, Name = "Date" },
            new { Id = 5, Name = "Time" },
            new { Id = 6, Name = "Decimal" },
            new { Id = 7, Name = "Rating" },
            new { Id = 8, Name = "Percentage" }
        };

        return Task.FromResult<object>(inputTypes);
    }

    private static string GetInputTypeName(int inputTypeId) => inputTypeId switch
    {
        1 => "Integer",
        2 => "String",
        3 => "Boolean",
        4 => "Date",
        5 => "Time",
        6 => "Decimal",
        7 => "Rating",
        8 => "Percentage",
        _ => "Unknown"
    };
}
