using LogMyDay.App.Authentication;
using LogMyDay.Shared.Interfaces;
using LogMyDay.Shared.Scanning;
using LogMyDay.Shared.Serialization;
using Refit;

namespace LogMyDay.App.Extensions;

internal static class RefitExtensions
{
    internal static IServiceCollection AddRefitClients(this IServiceCollection services, IConfiguration configuration)
    {
        var baseAddress = configuration["Api:BaseAddress"]
            ?? throw new InvalidOperationException("API base address (Api:BaseAddress) is not configured.");

        var refitSettings = new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(JsonSerializationSettings.CreateDefault())
        };

        services.AddRefitClient<IActivityApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseAddress))
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<IAuthApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseAddress))
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<IUsersApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseAddress))
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<IAccountApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseAddress))
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<ISecureBackupApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseAddress))
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<IAiApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseAddress))
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<ISettingsApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseAddress))
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<IScanMappingApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseAddress))
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddScoped<IScanOrchestrator, ScanOrchestrator>();

        return services;
    }
}
