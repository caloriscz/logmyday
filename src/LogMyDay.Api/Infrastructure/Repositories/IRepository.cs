using LogMyDay.Api.Infrastructure.Specifications;

namespace LogMyDay.Api.Infrastructure.Repositories;

/// <summary>
/// Generic repository interface for data access operations.
/// </summary>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Gets an entity by its ID.
    /// </summary>
    Task<T?> GetByIdAsync(int id);

    /// <summary>
    /// Gets all entities.
    /// </summary>
    Task<List<T>> GetAllAsync();

    /// <summary>
    /// Gets entities matching a specification.
    /// </summary>
    Task<List<T>> GetAsync(ISpecification<T> spec);

    /// <summary>
    /// Gets a single entity matching a specification.
    /// </summary>
    Task<T?> GetSingleAsync(ISpecification<T> spec);

    /// <summary>
    /// Gets the count of entities matching a specification.
    /// </summary>
    Task<int> CountAsync(ISpecification<T> spec);

    /// <summary>
    /// Checks if any entities match a specification.
    /// </summary>
    Task<bool> AnyAsync(ISpecification<T> spec);

    /// <summary>
    /// Adds a new entity.
    /// </summary>
    Task<T> AddAsync(T entity);

    /// <summary>
    /// Updates an existing entity.
    /// </summary>
    Task UpdateAsync(T entity);

    /// <summary>
    /// Deletes an entity.
    /// </summary>
    Task DeleteAsync(T entity);

    /// <summary>
    /// Saves all changes to the database.
    /// </summary>
    Task<int> SaveChangesAsync();
}
