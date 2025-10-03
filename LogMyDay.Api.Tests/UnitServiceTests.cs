using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace LogMyDay.Api.Tests;

public class UnitServiceTests
{
    [Fact]
    public async Task Create_ShouldPersistUnit()
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase("UnitService_Create")
            .Options;
        using var context = new LogMyDayDbContext(options);

        var quantity = new Quantity { Key = "test" };
        var baseUnit = new Unit { Key = "base", Symbol = "b", Quantity = quantity, AToBase = 1, BToBase = 0, Decimals = 0 };
        quantity.BaseUnit = baseUnit;
        context.Quantities.Add(quantity);
        context.Units.Add(baseUnit);
        await context.SaveChangesAsync();
        quantity.BaseUnitId = baseUnit.Id;
        await context.SaveChangesAsync();

        var logger = Mock.Of<ILogger<UnitService>>();
        var service = new UnitService(context, logger);
        var request = new UnitRequest
        {
            Key = "alternate",
            Symbol = "alt",
            QuantityId = quantity.Id,
            AToBase = 2,
            BToBase = 0,
            Decimals = 1
        };

        var id = await service.CreateAsync(request);
        var created = await context.Units.FindAsync(id);

        Assert.NotNull(created);
        Assert.Equal("alternate", created!.Key);
        Assert.Equal("alt", created.Symbol);
        Assert.Equal(quantity.Id, created.QuantityId);
    }

    [Fact]
    public async Task Delete_ShouldThrow_WhenDeletingBaseUnit()
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseInMemoryDatabase("UnitService_Delete")
            .Options;
        using var context = new LogMyDayDbContext(options);

        var quantity = new Quantity { Key = "time" };
        var baseUnit = new Unit { Key = "second", Symbol = "s", Quantity = quantity, AToBase = 1, BToBase = 0, Decimals = 0 };
        quantity.BaseUnit = baseUnit;
        context.Quantities.Add(quantity);
        context.Units.Add(baseUnit);
        await context.SaveChangesAsync();
        quantity.BaseUnitId = baseUnit.Id;
        await context.SaveChangesAsync();

        var logger = Mock.Of<ILogger<UnitService>>();
        var service = new UnitService(context, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(baseUnit.Id));
    }
}
