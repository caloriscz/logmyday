using System.Linq.Expressions;

namespace LogMyDay.Api.Infrastructure.Specifications;

/// <summary>
/// Base specification interface for building reusable query specifications.
/// </summary>
public interface ISpecification<T>
{
    /// <summary>
    /// Gets the filter expression to apply.
    /// </summary>
    Expression<Func<T, bool>>? Criteria { get; }

    /// <summary>
    /// Gets the list of includes (eager loading expressions).
    /// </summary>
    List<Expression<Func<T, object>>> Includes { get; }

    /// <summary>
    /// Gets the list of include strings (for ThenInclude scenarios).
    /// </summary>
    List<string> IncludeStrings { get; }

    /// <summary>
    /// Gets the order by expression.
    /// </summary>
    Expression<Func<T, object>>? OrderBy { get; }

    /// <summary>
    /// Gets the order by descending expression.
    /// </summary>
    Expression<Func<T, object>>? OrderByDescending { get; }

    /// <summary>
    /// Gets the secondary order by expression (ThenBy).
    /// </summary>
    Expression<Func<T, object>>? ThenOrderBy { get; }

    /// <summary>
    /// Gets the number of items to skip (for paging).
    /// </summary>
    int? Skip { get; }

    /// <summary>
    /// Gets the number of items to take (for paging).
    /// </summary>
    int? Take { get; }

    /// <summary>
    /// Gets whether the query should be split (for complex includes).
    /// </summary>
    bool IsSplitQuery { get; }
}
