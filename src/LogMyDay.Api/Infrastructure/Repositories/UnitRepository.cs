using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;

namespace LogMyDay.Api.Infrastructure.Repositories;

public class UnitRepository : Repository<Unit>, IUnitRepository
{
    public UnitRepository(LogMyDayDbContext context) : base(context)
    {
    }
}
