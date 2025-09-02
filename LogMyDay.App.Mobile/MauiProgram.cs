using CommunityToolkit.Maui;
using LogMyDay.App.Mobile.Services;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.DependencyInjection;

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

        // Add Blazor WebView
        builder.Services.AddMauiBlazorWebView();


        // Register essential services
        try
        {
            System.Diagnostics.Debug.WriteLine("MauiProgram: Starting service registration");

            // Register authentication service
            builder.Services.AddSingleton<AuthenticationService>();
            System.Diagnostics.Debug.WriteLine("MauiProgram: AuthenticationService registered");

            // Register server configuration service
            // Legacy ServerConfigurationService replaced by dynamic context/provider pattern
            System.Diagnostics.Debug.WriteLine("MauiProgram: (Deprecated) ServerConfigurationService skipped");

            // Dynamic API context & clients with auto-retry authentication
            builder.Services.Add(new ServiceDescriptor(typeof(IApiContext), typeof(ApiContext), ServiceLifetime.Singleton));
            builder.Services.Add(new ServiceDescriptor(typeof(AutoRetryAuthHandler), sp => 
                new AutoRetryAuthHandler(
                    sp.GetRequiredService<IApiContext>(), 
                    sp.GetRequiredService<AuthenticationService>()), ServiceLifetime.Transient));
            builder.Services.AddHttpClient("dynamic-api")
                .AddHttpMessageHandler<AutoRetryAuthHandler>();
            builder.Services.Add(new ServiceDescriptor(typeof(IApiClientProvider), typeof(ApiClientProvider), ServiceLifetime.Singleton));
            // Adapter so existing pages injecting IActivityApi continue to work
            builder.Services.AddTransient<LogMyDay.Shared.Interfaces.IActivityApi>(sp => sp.GetRequiredService<IApiClientProvider>().Activity);

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

            // Register notification services
#if ANDROID
            builder.Services.AddSingleton<INotificationManagerService, LogMyDay.App.Mobile.Platforms.Android.NotificationManagerService>();
#endif
            // Register the cross-platform wrapper service
            builder.Services.AddSingleton<NotificationService>();

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
