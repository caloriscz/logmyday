using CommunityToolkit.Maui;
using LogMyDay.App.Mobile.Services;
using LogMyDay.Shared.Interfaces;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LogMyDay.App.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        System.Diagnostics.Debug.WriteLine("MauiProgram: CreateMauiApp started");
        
        var builder = MauiApp.CreateBuilder();
        System.Diagnostics.Debug.WriteLine("MauiProgram: MauiApp builder created");
        
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);

        // Add Blazor WebView
        builder.Services.AddMauiBlazorWebView();


        // Register essential services
        try
        {
            System.Diagnostics.Debug.WriteLine("MauiProgram: Starting service registration");

            // Register authentication service - use the singleton instance
            builder.Services.AddSingleton<AuthenticationService>(provider => AuthenticationService.Instance);
            System.Diagnostics.Debug.WriteLine("MauiProgram: AuthenticationService registered as singleton instance");

            // Register server configuration service
            // Legacy ServerConfigurationService replaced by dynamic context/provider pattern
            System.Diagnostics.Debug.WriteLine("MauiProgram: (Deprecated) ServerConfigurationService skipped");

            // Dynamic API context & clients with dynamic authentication
            builder.Services.Add(new ServiceDescriptor(typeof(IApiContext), typeof(ApiContext), ServiceLifetime.Singleton));
            builder.Services.AddTransient<DynamicAuthHandler>();
            builder.Services.AddHttpClient("dynamic-api")
                .AddHttpMessageHandler<DynamicAuthHandler>();
            builder.Services.Add(new ServiceDescriptor(typeof(IApiClientProvider), typeof(ApiClientProvider), ServiceLifetime.Singleton));
            // Adapter so existing pages injecting API interfaces continue to work
            builder.Services.AddTransient<IActivityApi>(sp => sp.GetRequiredService<IApiClientProvider>().Activity);
            builder.Services.AddTransient<IAuthApi>(sp => sp.GetRequiredService<IApiClientProvider>().Auth);
            builder.Services.AddTransient<IUsersApi>(sp => sp.GetRequiredService<IApiClientProvider>().Users);
            builder.Services.AddTransient<IAccountApi>(sp => sp.GetRequiredService<IApiClientProvider>().Account);

            // Register app settings
            builder.Services.AddSingleton<AppSettings>(provider =>
            {
                var serverUrl = Preferences.Get("ServerUrl", "https://logmyday.tadata.cz");
                return new AppSettings { WebUrl = serverUrl, DefaultPage = "/" };
            });

            // Register authentication handler
            // Remove fixed Refit client; dynamic provider builds Refit clients on demand with selected server URL.
            System.Diagnostics.Debug.WriteLine("MauiProgram: Dynamic API client infrastructure registered");

            // Register other services
            builder.Services.AddScoped<ApiService>();
            builder.Services.AddScoped<QuickActivityService>();
            builder.Services.AddSingleton<IUserPreferencesService, UserPreferencesService>();
            builder.Services.AddSingleton<IPageTitleService, PageTitleService>();

            // Register notification services
#if ANDROID
            builder.Services.AddSingleton<INotificationManagerService, LogMyDay.App.Mobile.Platforms.Android.NotificationManagerService>();
#endif
            builder.Services.AddSingleton<NotificationNavigationService>();
            // Register the cross-platform wrapper service
            builder.Services.AddSingleton<NotificationService>();

            // Register system notification service
            builder.Services.AddSingleton<ISystemNotificationService, SystemNotificationService>();

            // Update App registration to include NotificationService dependency
            builder.Services.AddSingleton<App>();

        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MauiProgram: Error during service registration: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"MauiProgram: Stack trace: {ex.StackTrace}");
        }

        System.Diagnostics.Debug.WriteLine("MauiProgram: Building app...");
        var app = builder.Build();
        System.Diagnostics.Debug.WriteLine("MauiProgram: MauiApp built successfully");
        
        return app;
    }
}
