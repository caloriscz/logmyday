using LogMyDay.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Infrastructure.Data;

public static class DataSeederExtensions
{
    public static void SeedData(this ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<InputType>().HasData(
            new InputType { Id = 1, Name = "Integer" },
            new InputType { Id = 2, Name = "String" },
            new InputType { Id = 3, Name = "Boolean" },
            new InputType { Id = 4, Name = "Date" },
            new InputType { Id = 5, Name = "Time" },
            new InputType { Id = 6, Name = "Decimal, precision 2" }
        );
    }
}
