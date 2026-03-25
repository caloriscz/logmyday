namespace LogMyDay.Shared.Interfaces;

using LogMyDay.Shared.DTOs;
using Refit;

public interface IActivityApi
{
    [Get("/api/activities/{id}")]
    Task<ActivityResponse> GetCalendarById(int id);

    [Get("/api/activities")]
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

    [Get("/api/activities/by-year")]
    Task<List<ActivityResponse>> GetActivitiesByYear(int year, int? tagId = null);

    [Get("/api/activities/available-years")]
    Task<List<int>> GetAvailableYears(int? tagId = null);

    [Post("/api/activities")]
    Task<ActivityResponse> CreateCalendarItem([Body] ActivityRequest calendarRequest);

    [Put("/api/activities/{id}")]
    Task<ActivityResponse> UpdateCalendarItem(int id, [Body] ActivityRequest request);

    [Delete("/api/activities/{id}")]
    Task Delete(int id);

    [Post("/api/activities/by-date")]
    Task<List<ActivityResponse>> GetCalendarByDate(ActivityRequest request);

    [Get("/api/activities/check-duplicate")]
    Task<DuplicateCheckResponse> CheckDuplicate(int tagId, DateTime dateStarted, int? activityId = null);

    [Get("/api/activities/required-daily-tags-unfilled")]
    // Legacy endpoint: kept for future experiments with required-tag reminders. (in the future will be replaced by Notifications)
    Task<List<TagResponse>> GetRequiredDailyTagsNotFilledForDate([Query] string dateString);

    [Get("/api/activities/has-activity-for-tag")]
    Task<bool> HasActivityForTag([Query] int tagId, [Query] DateOnly date);

    [Get("/api/tags/paged")]
    Task<PagedResult<TagResponse>> GetTagsPaged(
        int pageNumber = 1,
        int pageSize = 20,
        string orderBy = "asc",
        string? filter = null,
        string? filterType = null,
        int? groupId = null);

    [Get("/api/tags")]
    Task<IList<TagResponse>> GetTags();

    [Get("/api/tags/{id}")]
    Task<TagResponse> GetTagById(int id);

    [Post("/api/tags")]
    Task<int> CreateTag([Body] TagRequest tagRequest);

    [Put("/api/tags/{id}")]
    Task UpdateTag(int id, [Body] TagRequest tagRequest);

    [Delete("/api/tags/{id}")]
    Task DeleteTag(int id);

    [Get("/api/units")]
    Task<IList<UnitResponse>> GetUnitsAsync();

    [Get("/api/units/quantities")]
    Task<IList<QuantityResponse>> GetQuantitiesAsync();

    [Post("/api/units")]
    Task<int> CreateUnit([Body] UnitRequest request);

    [Put("/api/units/{id}")]
    Task UpdateUnit(int id, [Body] UnitRequest request);

    [Delete("/api/units/{id}")]
    Task DeleteUnit(int id);

    [Get("/api/tagoptionlists")]
    Task<IList<TagOptionListResponse>> GetTagOptionListsAsync();

    [Post("/api/tagoptionlists")]
    Task<int> CreateTagOptionList([Body] TagOptionListRequest request);

    [Put("/api/tagoptionlists/{id}")]
    Task UpdateTagOptionList(int id, [Body] TagOptionListRequest request);

    [Delete("/api/tagoptionlists/{id}")]
    Task DeleteTagOptionList(int id);

    [Get("/api/input-types")]
    Task<List<InputTypeDto>> GetInputTypesAsync();

    [Get("/api/notifications")]
    Task<IList<NotificationResponse>> GetNotifications();

    [Get("/api/notifications/tag/{tagId}")]
    Task<IList<NotificationResponse>> GetNotificationsByTag(int tagId);

    [Post("/api/notifications")]
    Task<NotificationResponse> CreateNotification([Body] NotificationRequest request);

    [Put("/api/notifications/{id}")]
    Task<NotificationResponse> UpdateNotification(int id, [Body] NotificationRequest request);

    [Delete("/api/notifications/{id}")]
    Task DeleteNotification(int id);

    [Post("/api/notifications/{id}/deliveries")]
    Task<NotificationResponse> RecordNotificationDelivery(int id, [Body] NotificationDeliveryRequest request);
}

