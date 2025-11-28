using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Api.Infrastructure.Specifications;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Infrastructure.Repositories;

/// <summary>
/// Generic repository implementation using EF Core.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly LogMyDayDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(LogMyDayDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public virtual async Task<List<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public virtual async Task<List<T>> GetAsync(ISpecification<T> spec)
    {
        return await ApplySpecification(spec).ToListAsync();
    }

    public virtual async Task<T?> GetSingleAsync(ISpecification<T> spec)
    {
        return await ApplySpecification(spec).FirstOrDefaultAsync();
    }

    public virtual async Task<int> CountAsync(ISpecification<T> spec)
    {
        return await ApplySpecification(spec).CountAsync();
    }

    public virtual async Task<bool> AnyAsync(ISpecification<T> spec)
    {
        return await ApplySpecification(spec).AnyAsync();
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        
        return entity;
    }

    public virtual async Task UpdateAsync(T entity)
    {
        _context.Entry(entity).State = EntityState.Modified;
        await Task.CompletedTask;
    }

    public virtual async Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        await Task.CompletedTask;
    }

    public virtual async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Applies the specification to the query.
    /// </summary>
    protected IQueryable<T> ApplySpecification(ISpecification<T> spec)
    {
        return SpecificationEvaluator<T>.GetQuery(_dbSet.AsQueryable(), spec);
    }
}

/// <summary>
/// Helper class to evaluate specifications and build EF Core queries.
/// </summary>
public static class SpecificationEvaluator<T> where T : class
{
    public static IQueryable<T> GetQuery(IQueryable<T> inputQuery, ISpecification<T> spec)
    {
        var query = inputQuery;

        // Apply criteria (WHERE clause)
        if (spec.Criteria != null)
        {
            query = query.Where(spec.Criteria);
        }

        // Apply includes (eager loading)
        query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));

        // Apply include strings (for ThenInclude scenarios)
        query = spec.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));

        // Apply split query if specified
        if (spec.IsSplitQuery)
        {
            query = query.AsSplitQuery();
        }

        // Apply ordering
        if (spec.OrderBy != null)
        {
            query = query.OrderBy(spec.OrderBy);
        }
        else if (spec.OrderByDescending != null)
        {
            query = query.OrderByDescending(spec.OrderByDescending);
        }

        // Apply paging
        if (spec.Skip.HasValue)
        {
            query = query.Skip(spec.Skip.Value);
        }

        if (spec.Take.HasValue)
        {
            query = query.Take(spec.Take.Value);
        }

        return query;
    }
}
