using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IActivityService
{
    Task<ActivityResponse> GetById(int id);
    Task<List<ActivityResponse>> GetAll();
    Task<ActivityResponse> Create(ActivityRequest calendarRequest);
    Task<ActivityResponse> Update(int id, DateTime dateCreated, DateTime? dateFinished);
    Task<bool> Delete(int id);
    Task<List<ActivityResponse>> GetByDate(ActivityRequest request);
    Task<PagedResult<ActivityResponse>> GetPaged(
        int pageNumber,
        int pageSize,
        string orderBy,
        int? tagId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? descriptionFilter = null
    );
    Task<PagedResult<ActivityResponse>> GetPagedByWeeks(
        int weekPageNumber,
        int weeksPerPage,
        string orderBy,
        int? tagId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? descriptionFilter = null
    );
    Task<PagedResult<ActivityResponse>> GetPagedByMonths(
        int monthPageNumber,
        int monthsPerPage,
        string orderBy,
        int? tagId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? descriptionFilter = null
    );
    Task<List<ActivityResponse>> GetByYear(int year, int? tagId = null);
    Task<List<int>> GetAvailableYears(int? tagId = null);
    Task<bool> HasActivityForTimeGranularity(int tagId, DateTime dateStarted);
    Task<List<TagResponse>> GetRequiredDailyTagsNotFilledForDate(DateTime date);
}
