using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Activity entity with custom query methods.
/// </summary>
public class ActivityRepository : Repository<Activity>, IActivityRepository
{
    public ActivityRepository(LogMyDayDbContext context) : base(context)
    {
    }

    public async Task<List<int>> GetAvailableYearsAsync(Guid userId, int? tagId = null)
    {
        var query = _dbSet.Where(a => a.UserId == userId).AsQueryable();

        if (tagId.HasValue)
        {
            query = query.Where(a => a.TagId == tagId.Value);
        }

        var years = await query
            .Select(a => a.DateStarted.Year)
            .Distinct()
            .OrderDescending()
            .ToListAsync();

        return years;
    }

}
