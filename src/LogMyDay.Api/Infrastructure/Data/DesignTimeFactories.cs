namespace LogMyDay.Api.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

// The factories serve "dotnet ef migrations add" only. Scaffolding needs the provider and the
// model, not a database: the app itself migrates the platform database and every user's
// database when it starts.

/// <summary>Design-time factory of the platform context.</summary>
public sealed class PlatformDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        return new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(DesignTime.ConnectionString).Options);
    }
}

/// <summary>Design-time factory of the data context.</summary>
public sealed class LogMyDayDbContextFactory : IDesignTimeDbContextFactory<LogMyDayDbContext>
{
    public LogMyDayDbContext CreateDbContext(string[] args)
    {
        return new LogMyDayDbContext(new DbContextOptionsBuilder<LogMyDayDbContext>().UseSqlite(DesignTime.ConnectionString).Options);
    }
}

internal static class DesignTime
{
    public const string ConnectionString = "Data Source=:memory:";
}
