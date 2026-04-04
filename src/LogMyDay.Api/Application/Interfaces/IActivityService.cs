using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IActivityService
{
    Task<ActivityResponse> GetById(int id, Guid userId);
    Task<List<ActivityResponse>> GetAll(Guid userId);
    Task<ActivityResponse> Create(ActivityRequest calendarRequest, Guid userId);
    Task<ActivityResponse> Update(int id, ActivityRequest request, Guid userId);
    Task<bool> Delete(int id, Guid userId);
    Task<List<ActivityResponse>> GetByDate(ActivityRequest request, Guid userId);
    Task<PagedResult<ActivityResponse>> GetPaged(
        int pageNumber,
        int pageSize,
        string orderBy,
        Guid userId,
        int? tagId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? descriptionFilter = null
    );
    Task<PagedResult<ActivityResponse>> GetPagedByWeeks(
        int weekPageNumber,
        int weeksPerPage,
        string orderBy,
        Guid userId,
        int? tagId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? descriptionFilter = null
    );
    Task<PagedResult<ActivityResponse>> GetPagedByMonths(
        int monthPageNumber,
        int monthsPerPage,
        string orderBy,
        Guid userId,
        int? tagId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? descriptionFilter = null
    );
    Task<List<ActivityResponse>> GetByYear(int year, Guid userId, int? tagId = null);
    Task<List<int>> GetAvailableYears(Guid userId, int? tagId = null);
    Task<bool> HasActivityForTimeGranularity(int tagId, DateTime dateStarted, Guid userId, int? excludeActivityId = null);
    Task<bool> HasActivityForTagOnDate(int tagId, DateOnly date, Guid userId);
    Task<List<TagResponse>> GetRequiredDailyTagsNotFilledForDate(DateTime date, Guid userId);
    Task<PeriodSumResponse> GetPeriodSum(int tagId, DateTime dateStarted, Guid userId, int? excludeActivityId = null);
}
