using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;

namespace LogMyDay.Api.Infrastructure.Repositories;

public class TagRepository : Repository<Tag>, ITagRepository
{
    public TagRepository(LogMyDayDbContext context) : base(context)
    {
    }
}
