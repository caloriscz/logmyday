using System.Linq.Expressions;

namespace LogMyDay.Api.Infrastructure.Specifications;

/// <summary>
/// Base implementation of ISpecification with fluent API for building query specifications.
/// </summary>
public abstract class BaseSpecification<T> : ISpecification<T>
{
    protected BaseSpecification()
    {
    }

    protected BaseSpecification(Expression<Func<T, bool>> criteria)
    {
        Criteria = criteria;
    }

    public Expression<Func<T, bool>>? Criteria { get; private set; }
    public List<Expression<Func<T, object>>> Includes { get; } = new();
    public List<string> IncludeStrings { get; } = new();
    public Expression<Func<T, object>>? OrderBy { get; private set; }
    public Expression<Func<T, object>>? OrderByDescending { get; private set; }
    public Expression<Func<T, object>>? ThenOrderBy { get; private set; }
    public int? Skip { get; private set; }
    public int? Take { get; private set; }
    public bool IsSplitQuery { get; private set; }

    protected void AddInclude(Expression<Func<T, object>> includeExpression)
    {
        Includes.Add(includeExpression);
    }

    protected void AddInclude(string includeString)
    {
        IncludeStrings.Add(includeString);
    }

    protected void ApplyPaging(int skip, int take)
    {
        Skip = skip;
        Take = take;
    }

    protected void ApplyOrderBy(Expression<Func<T, object>> orderByExpression)
    {
        OrderBy = orderByExpression;
    }

    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescExpression)
    {
        OrderByDescending = orderByDescExpression;
    }

    protected void ApplyThenOrderBy(Expression<Func<T, object>> thenOrderByExpression)
    {
        ThenOrderBy = thenOrderByExpression;
    }

    protected void EnableSplitQuery()
    {
        IsSplitQuery = true;
    }

    protected void AddCriteria(Expression<Func<T, bool>> criteriaExpression)
    {
        if (Criteria == null)
        {
            Criteria = criteriaExpression;
        }
        else
        {
            // Combine existing criteria with AND
            var parameter = Expression.Parameter(typeof(T));
            var combined = Expression.AndAlso(
                Expression.Invoke(Criteria, parameter),
                Expression.Invoke(criteriaExpression, parameter)
            );
            Criteria = Expression.Lambda<Func<T, bool>>(combined, parameter);
        }
    }
}
