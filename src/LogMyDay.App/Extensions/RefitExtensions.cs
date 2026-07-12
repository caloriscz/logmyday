using LogMyDay.App.Authentication;
using LogMyDay.Shared.Interfaces;
using LogMyDay.Shared.Scanning;
using LogMyDay.Shared.Serialization;
using Refit;

namespace LogMyDay.App.Extensions;

internal static class RefitExtensions
{
    // Placeholder used as BaseAddress so Refit can form absolute URIs; the actual host/scheme/port
    // is overwritten by SelfBaseAddressHandler at request time using the current HttpContext.
    private static readonly Uri PlaceholderBaseAddress = new("http://localhost");

    internal static IServiceCollection AddRefitClients(this IServiceCollection services, IConfiguration configuration)
    {
        // Api:BaseAddress is now optional — kept for backward compatibility only.
        // When present it is used as the placeholder; otherwise http://localhost is used.
        var explicitBaseAddress = configuration["Api:BaseAddress"];
        var baseAddress = !string.IsNullOrWhiteSpace(explicitBaseAddress)
            ? new Uri(explicitBaseAddress)
            : PlaceholderBaseAddress;

        services.AddScoped<SelfBaseAddressHandler>();

        var refitSettings = new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(JsonSerializationSettings.CreateDefault())
        };

        services.AddRefitClient<IActivityApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<SelfBaseAddressHandler>()
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<IAuthApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<SelfBaseAddressHandler>()
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<IUsersApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<SelfBaseAddressHandler>()
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<IAccountApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<SelfBaseAddressHandler>()
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<ISecureBackupApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<SelfBaseAddressHandler>()
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<IAiApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<SelfBaseAddressHandler>()
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<ISettingsApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<SelfBaseAddressHandler>()
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<IScanMappingApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<SelfBaseAddressHandler>()
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<ITagGroupApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<SelfBaseAddressHandler>()
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<IColorSchemeApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<SelfBaseAddressHandler>()
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<IEventLogApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<SelfBaseAddressHandler>()
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<ITodoApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<SelfBaseAddressHandler>()
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<ITagDayLockApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<SelfBaseAddressHandler>()
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddRefitClient<IReminderApi>(refitSettings)
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<SelfBaseAddressHandler>()
            .AddHttpMessageHandler<CookieAuthenticationHandler>();

        services.AddScoped<IScanOrchestrator, ScanOrchestrator>();

        return services;
    }
}
