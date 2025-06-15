namespace LogMyDay.Shared.Interfaces;

using LogMyDay.Shared.DTOs;
using Refit;

public interface IActivityApi
{
    [Get("/api/activities/{id}")]
    Task<ActivityResponse> GetCalendarById(int id);    [Get("/api/activities")]
    Task<PagedResult<ActivityResponse>> GetActivities(
        int pageNumber = 1,
        int pageSize = 20,
        string orderBy = "desc",
        int? tagId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? descriptionFilter = null);

    [Get("/api/activities/paged-by-weeks")]
    Task<PagedResult<ActivityResponse>> GetActivitiesByWeeks(
        int weekPageNumber = 1,
        int weeksPerPage = 12,
        string orderBy = "desc",
        int? tagId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? descriptionFilter = null);

    [Get("/api/activities/paged-by-months")]
    Task<PagedResult<ActivityResponse>> GetActivitiesByMonths(
        int monthPageNumber = 1,
        int monthsPerPage = 12,
        string orderBy = "desc",
        int? tagId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? descriptionFilter = null);

    [Post("/api/activities")]
    Task<ActivityResponse> CreateCalendarItem([Body] ActivityRequest calendarRequest);

    [Put("/api/activities/{id}")]
    Task<ActivityResponse> UpdateCalendarItem(int id, [Body] DateTime dateCreated, DateTime? dateFinished);

    [Delete("/api/activities/{id}")]
    Task Delete(int id);    [Post("/api/activities/by-date")]
    Task<List<ActivityResponse>> GetCalendarByDate(ActivityRequest request);    [Get("/api/activities/check-duplicate")]
    Task<DuplicateCheckResponse> CheckDuplicate(int tagId, DateTime dateStarted);

    [Get("/api/tags/paged")]
    Task<PagedResult<TagResponse>> GetTagsPaged(
        int pageNumber = 1,
        int pageSize = 20,
        string orderBy = "asc",
        string? filter = null,
        string? filterType = null);

    [Get("/api/tags")]
    Task<IList<TagResponse>> GetTags();

    [Get("/api/tags/{id}")]
    Task<TagResponse> GetTagById(int id);

    [Post("/api/tags")]
    Task CreateTag([Body] TagRequest tagRequest);

    [Put("/api/tags/{id}")]
    Task UpdateTag(int id, [Body] TagRequest tagRequest);

    [Delete("/api/tags/{id}")]
    Task DeleteTag(int id);

    [Get("/api/input-types")]
    Task<List<InputTypeDto>> GetInputTypesAsync();
}

