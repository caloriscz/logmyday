using LogMyDay.Domain.Entities;

namespace LogMyDay.Api.Infrastructure.Repositories;

/// <summary>
/// Repository interface for Activity-specific operations.
/// Extends the generic repository with activity-specific methods.
/// </summary>
public interface IActivityRepository : IRepository<Activity>
{
    /// <summary>
    /// Gets activities for a specific year with optional tag filter.
    /// </summary>
    Task<List<int>> GetAvailableYearsAsync(Guid userId, int? tagId = null);

}
