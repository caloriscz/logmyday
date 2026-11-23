using LogMyDay.Api.Application.Services;
using LogMyDay.Api.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LogMyDay.Api.Tests;

/// <summary>
/// Every user's database starts with the default quantities and units, each quantity with its
/// base unit. A seed that is interrupted leaves nothing behind, so the next attempt seeds again.
/// </summary>
public sealed class DefaultUnitsTests : IDisposable
{
    private readonly string _folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"lmd-default-units-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void ANewDatabase_GetsEveryQuantityWithItsBaseUnit()
    {
        using (var db = Context())
        {
            db.Database.Migrate();
            DefaultUnits.Ensure(db);
        }

        using var check = Context();
        Assert.NotEmpty(check.Quantities);
        Assert.All(check.Quantities, quantity => Assert.NotNull(quantity.BaseUnitId));
    }

    // The seed saves the quantities, then the units, then each quantity's base unit.
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void ASeedThatIsInterrupted_LeavesNothing_AndTheNextOneCompletes(int failingSave)
    {
        using (var db = Context(new FailingSave(failingSave)))
        {
            db.Database.Migrate();
            Assert.Throws<IOException>(() => DefaultUnits.Ensure(db));
        }

        using (var check = Context())
        {
            Assert.Empty(check.Quantities);
            Assert.Empty(check.Units);
        }

        using (var retry = Context())
        {
            DefaultUnits.Ensure(retry);
        }

        using var done = Context();
        Assert.Equal(done.Quantities.Select(q => q.Key).Distinct().Count(), done.Quantities.Count());
        Assert.All(done.Quantities, quantity => Assert.NotNull(quantity.BaseUnitId));
    }

    private LogMyDayDbContext Context(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<LogMyDayDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_folder, "user.db")}")
            .AddInterceptors(interceptors)
            .Options;

        return new LogMyDayDbContext(options);
    }

    /// <summary>Stops the process, as far as the database can tell, at the given save.</summary>
    private sealed class FailingSave(int failingSave) : SaveChangesInterceptor
    {
        private int _saves;

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (++_saves == failingSave)
            {
                throw new IOException("The process stopped.");
            }

            return result;
        }
    }
}
