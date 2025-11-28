using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;

namespace LogMyDay.Api.Infrastructure.Repositories;

public class QuantityRepository : Repository<Quantity>, IQuantityRepository
{
    public QuantityRepository(LogMyDayDbContext context) : base(context)
    {
    }
}
