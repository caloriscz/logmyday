using LogMyDay.Domain.Entities;

namespace LogMyDay.Api.Infrastructure.Specifications;

public static class ColorSchemeSpecifications
{
    public class ColorSchemesForUserSpec : BaseSpecification<ColorScheme>
    {
        public ColorSchemesForUserSpec(Guid userId)
            : base(s => s.UserId == userId)
        {
            AddInclude(s => s.Entries);
            ApplyOrderBy(s => s.Name);
        }
    }

    public class ColorSchemeByIdAndUserSpec : BaseSpecification<ColorScheme>
    {
        public ColorSchemeByIdAndUserSpec(int id, Guid userId)
            : base(s => s.Id == id && s.UserId == userId)
        {
            AddInclude(s => s.Entries);
        }
    }
}
