using LogMyDay.Api.Infrastructure.Specifications;
using LogMyDay.Domain.Entities;

namespace LogMyDay.Api.Infrastructure.Specifications;

public static class TagGroupSpecifications
{
    public class TagGroupsForUserSpec : BaseSpecification<TagGroup>
    {
        public TagGroupsForUserSpec(Guid userId)
            : base(g => g.UserId == userId)
        {
            ApplyOrderBy(g => g.Name);
        }
    }

    public class TagGroupByIdAndUserSpec : BaseSpecification<TagGroup>
    {
        public TagGroupByIdAndUserSpec(int id, Guid userId)
            : base(g => g.Id == id && g.UserId == userId)
        {
        }
    }
}
