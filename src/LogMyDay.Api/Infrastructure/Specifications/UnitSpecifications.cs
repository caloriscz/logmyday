using LogMyDay.Domain.Entities;

namespace LogMyDay.Api.Infrastructure.Specifications;

public static class UnitSpecifications
{
    /// <summary>
    /// Specification to get all units with Quantity and BaseUnit relationships
    /// </summary>
    public class UnitsWithQuantitySpec : BaseSpecification<Unit>
    {
        public UnitsWithQuantitySpec() : base(u => true)
        {
            AddInclude(u => u.Quantity);
            AddInclude("Quantity.BaseUnit"); // ThenInclude as string path
            // Note: OrderBy Quantity.Key then by Unit.Key would require custom implementation
            // For now, just order by Unit.Key
            ApplyOrderBy(u => u.Key);
        }
    }

    /// <summary>
    /// Specification to get a single unit by ID with Quantity and BaseUnit relationships
    /// </summary>
    public class UnitByIdSpec : BaseSpecification<Unit>
    {
        public UnitByIdSpec(int unitId) : base(u => u.Id == unitId)
        {
            AddInclude(u => u.Quantity);
            AddInclude("Quantity.BaseUnit"); // ThenInclude as string path
        }
    }

    /// <summary>
    /// Specification to find unit by key and quantity (for uniqueness checks)
    /// </summary>
    public class UnitByKeyAndQuantitySpec : BaseSpecification<Unit>
    {
        public UnitByKeyAndQuantitySpec(string key, int quantityId, int? excludeId = null)
            : base(u => u.QuantityId == quantityId && u.Key == key)
        {
            if (excludeId.HasValue)
            {
                AddCriteria(u => u.Id != excludeId.Value);
            }
        }
    }

    /// <summary>
    /// Specification to get all quantities with BaseUnit relationship
    /// </summary>
    public class QuantitiesWithBaseUnitSpec : BaseSpecification<Quantity>
    {
        public QuantitiesWithBaseUnitSpec() : base(q => true)
        {
            AddInclude(q => q.BaseUnit);
            ApplyOrderBy(q => q.Key);
        }
    }

    /// <summary>
    /// Specification to check if a quantity exists by ID
    /// </summary>
    public class QuantityByIdSpec : BaseSpecification<Quantity>
    {
        public QuantityByIdSpec(int quantityId) : base(q => q.Id == quantityId)
        {
        }
    }

    /// <summary>
    /// Specification to check if a unit is used by any tags
    /// </summary>
    public class TagsUsingUnitSpec : BaseSpecification<Tag>
    {
        public TagsUsingUnitSpec(int unitId) : base(t => t.UnitId == unitId)
        {
        }
    }

    /// <summary>
    /// Specification to get unit with its quantity to check if it's a base unit
    /// </summary>
    public class UnitWithQuantityForDeleteSpec : BaseSpecification<Unit>
    {
        public UnitWithQuantityForDeleteSpec(int unitId) : base(u => u.Id == unitId)
        {
            AddInclude(u => u.Quantity);
        }
    }
}
