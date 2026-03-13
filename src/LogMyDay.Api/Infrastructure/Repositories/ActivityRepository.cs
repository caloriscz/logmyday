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

    public async Task<List<Tag>> GetRequiredDailyTagsNotFilledAsync(DateTime date, Guid userId)
    {
        var startOfDay = date.Date;
        var endOfDay = date.Date.AddDays(1).AddTicks(-1);
        var unfilledTags = await _context.Tags
             .Where(t =>
                 t.UserId == userId &&
                 t.IsRequired
             )
             .Where(t => !_context.Activities.Any(a =>
                 a.UserId == userId &&
                 a.TagId == t.Id &&
                 a.DateStarted >= startOfDay &&
                 a.DateStarted <= endOfDay
             ))
             .Include(t => t.InputType)
             .Include(t => t.Unit)
             .Include(t => t.OptionList)
             .Include(t => t.Group)
             .ToListAsync();

        return unfilledTags;
    }
}
