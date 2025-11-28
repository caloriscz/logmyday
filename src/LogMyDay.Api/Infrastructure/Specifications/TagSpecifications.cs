using LogMyDay.Domain.Entities;

namespace LogMyDay.Api.Infrastructure.Specifications;

public static class TagSpecifications
{
    /// <summary>
    /// Specification to get all tags for a specific user, ordered by TagName
    /// </summary>
    public class TagsForUserSpec : BaseSpecification<Tag>
    {
        public TagsForUserSpec(Guid userId) 
            : base(t => t.UserId == userId)
        {
            AddInclude(t => t.Unit);
            AddInclude(t => t.OptionList);
            ApplyOrderBy(t => t.TagName);
        }
    }

    /// <summary>
    /// Specification to get a single tag by ID and user ID
    /// </summary>
    public class TagByIdAndUserSpec : BaseSpecification<Tag>
    {
        public TagByIdAndUserSpec(int tagId, Guid userId)
            : base(t => t.Id == tagId && t.UserId == userId)
        {
            AddInclude(t => t.Unit);
            AddInclude(t => t.OptionList);
        }
    }

    /// <summary>
    /// Specification for paginated, sorted, and filtered tags
    /// </summary>
    public class PagedTagsSpec : BaseSpecification<Tag>
    {
        public PagedTagsSpec(
            Guid userId,
            int pageNumber,
            int pageSize,
            string? orderBy = null,
            string? filter = null,
            string? filterType = null)
            : base(t => t.UserId == userId)
        {
            AddInclude(t => t.Unit);
            AddInclude(t => t.OptionList);

            // Apply filter if provided
            if (!string.IsNullOrWhiteSpace(filter))
            {
                if (filterType == "exact")
                {
                    AddCriteria(t => t.TagName == filter);
                }
                else
                {
                    AddCriteria(t => t.TagName.Contains(filter));
                }
            }

            // Apply ordering
            if (orderBy?.ToLower() == "asc")
            {
                ApplyOrderBy(t => t.TagName);
            }
            else
            {
                ApplyOrderByDescending(t => t.TagName);
            }

            // Apply pagination
            ApplyPaging((pageNumber - 1) * pageSize, pageSize);
        }
    }

    /// <summary>
    /// Specification to count tags with optional filter (for pagination total count)
    /// </summary>
    public class TagCountSpec : BaseSpecification<Tag>
    {
        public TagCountSpec(
            Guid userId,
            string? filter = null,
            string? filterType = null)
            : base(t => t.UserId == userId)
        {
            // Apply filter if provided (same logic as PagedTagsSpec)
            if (!string.IsNullOrWhiteSpace(filter))
            {
                if (filterType == "exact")
                {
                    AddCriteria(t => t.TagName == filter);
                }
                else
                {
                    AddCriteria(t => t.TagName.Contains(filter));
                }
            }
        }
    }
}
