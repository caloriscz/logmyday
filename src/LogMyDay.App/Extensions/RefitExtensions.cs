using LogMyDay.App.Authentication;
using LogMyDay.Shared.Interfaces;
using LogMyDay.Shared.Scanning;
using LogMyDay.Shared.Serialization;
using Microsoft.AspNetCore.Components.Server.Circuits;
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

        // The connections are shared by all users, so they keep no cookies.
        services.AddHttpClient(ApiHttpClient.Connections)
            .UseSocketsHttpHandler((handler, _) => handler.UseCookies = false);
        services.AddScoped(sp => ActivatorUtilities.CreateInstance<ApiHttpClient>(sp, baseAddress));
        services.AddScoped<CircuitHandler, CircuitOriginHandler>();

        var refitSettings = new RefitSettings
        {
            ContentSerializer = new SystemTextJsonContentSerializer(JsonSerializationSettings.CreateDefault())
        };

        services.AddApi<IActivityApi>(refitSettings);
        services.AddApi<IAuthApi>(refitSettings);
        services.AddApi<IUsersApi>(refitSettings);
        services.AddApi<IAccountApi>(refitSettings);
        services.AddApi<ISecureBackupApi>(refitSettings);
        services.AddApi<IAiApi>(refitSettings);
        services.AddApi<ISettingsApi>(refitSettings);
        services.AddApi<IScanMappingApi>(refitSettings);
        services.AddApi<ITagGroupApi>(refitSettings);
        services.AddApi<IColorSchemeApi>(refitSettings);
        services.AddApi<ITagRuleApi>(refitSettings);
        services.AddApi<ITagCheckApi>(refitSettings);
        services.AddApi<ITagTriggerApi>(refitSettings);
        services.AddApi<IRecordApi>(refitSettings);
        services.AddApi<IEventLogApi>(refitSettings);
        services.AddApi<ITodoApi>(refitSettings);
        services.AddApi<ITagDayLockApi>(refitSettings);
        services.AddApi<IApiKeysApi>(refitSettings);
        services.AddApi<IReminderApi>(refitSettings);

        services.AddScoped<IScanOrchestrator, ScanOrchestrator>();

        return services;
    }

    private static void AddApi<TApi>(this IServiceCollection services, RefitSettings refitSettings)
        where TApi : class
    {
        services.AddScoped(sp => RestService.For<TApi>(sp.GetRequiredService<ApiHttpClient>().Client, refitSettings));
    }
}
