namespace LogMyDay.Api.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

public static class DatabaseRegistration
{
    /// <summary>
    /// The services that say whose data a scope works on. Registered in every environment; what a
    /// data context does with the answer depends on how the contexts are registered.
    /// </summary>
    public static IServiceCollection AddUserScopes(this IServiceCollection services)
    {
        services.TryAddScoped<UserDatabaseScope>();
        services.TryAddSingleton<IUserScopeFactory, UserScopeFactory>();
        services.TryAddSingleton<IUserDataStore, SharedUserDataStore>();

        return services;
    }

    /// <summary>
    /// Registers the platform database (the configured file) and the data context, which opens
    /// the database of the user its scope works for.
    /// </summary>
    /// <param name="platformConnectionString">The connection string of the platform database, with an absolute file path.</param>
    public static IServiceCollection AddLogMyDayDatabases(this IServiceCollection services, string platformConnectionString)
    {
        services.AddUserScopes();

        services.AddSingleton(sp => new UserDatabases(platformConnectionString, sp.GetRequiredService<ILogger<UserDatabases>>()));
        services.Replace(ServiceDescriptor.Singleton<IUserDataStore>(sp => sp.GetRequiredService<UserDatabases>()));

        services.AddDbContext<PlatformDbContext>(options => options.UseSqlite(platformConnectionString));

        services.AddDbContext<LogMyDayDbContext>((sp, options) =>
        {
            var userId = sp.GetRequiredService<UserDatabaseScope>().UserId;
            if (userId is not { } owner)
            {
                UserDatabases.ConfigureWithoutUser(options);

                return;
            }

            var databases = sp.GetRequiredService<UserDatabases>();
            databases.Ensure(owner);
            databases.Configure(options, owner);
        });

        return services;
    }
}
