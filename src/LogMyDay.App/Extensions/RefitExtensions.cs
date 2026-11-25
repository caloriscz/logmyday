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

        // The connections are shared by all users, so they keep no cookies. They go to this app only.
        services.AddSingleton<OwnAddress>();
        services.AddHttpClient(ApiHttpClient.Connections)
            .UseSocketsHttpHandler((handler, sp) =>
            {
                var own = sp.GetRequiredService<OwnAddress>();
                handler.UseCookies = false;
                handler.UseProxy = !own.IsKnown;
                handler.ConnectCallback = own.Connect;
            });
        services.AddScoped(sp => ActivatorUtilities.CreateInstance<ApiHttpClient>(sp, baseAddress));
        services.AddSingleton<CircuitConnection>();
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
        // The builder holds what Refit reads from the interface by reflection; it has no state of a user.
        services.AddSingleton(_ => RequestBuilder.ForType<TApi>(refitSettings));
        services.AddScoped(sp => RestService.For(sp.GetRequiredService<ApiHttpClient>().Client, sp.GetRequiredService<IRequestBuilder<TApi>>()));
    }
}
