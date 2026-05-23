using System.Net.Http;
using CommunityToolkit.Maui;
using LogMyDay.App.Mobile.Services;
using LogMyDay.Shared.Interfaces;
using LogMyDay.Shared.Scanning;
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

        builder.ConfigureMauiHandlers(handlers =>
        {
#if ANDROID
            handlers.AddHandler(typeof(LogMyDay.App.Mobile.Controls.CustomRefreshView), typeof(LogMyDay.App.Mobile.Platforms.Android.CustomRefreshViewHandler));
            handlers.AddHandler<BlazorWebView, LogMyDay.App.Mobile.Platforms.Android.Handlers.CameraEnabledBlazorHandler>();
#endif
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
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                    MaxConnectionsPerServer = 4,
                    EnableMultipleHttp2Connections = true
                })
                .AddHttpMessageHandler<DynamicAuthHandler>();
            builder.Services.Add(new ServiceDescriptor(typeof(IApiClientProvider), typeof(ApiClientProvider), ServiceLifetime.Singleton));
            // Adapter so existing pages injecting API interfaces continue to work
            builder.Services.AddTransient<IActivityApi>(sp => sp.GetRequiredService<IApiClientProvider>().Activity);
            builder.Services.AddTransient<IAuthApi>(sp => sp.GetRequiredService<IApiClientProvider>().Auth);
            builder.Services.AddTransient<IUsersApi>(sp => sp.GetRequiredService<IApiClientProvider>().Users);
            builder.Services.AddTransient<IAccountApi>(sp => sp.GetRequiredService<IApiClientProvider>().Account);
            builder.Services.AddTransient<IScanMappingApi>(sp => sp.GetRequiredService<IApiClientProvider>().ScanMapping);
            builder.Services.AddTransient<IAiApi>(sp => sp.GetRequiredService<IApiClientProvider>().Ai);
            builder.Services.AddTransient<ITodoApi>(sp => sp.GetRequiredService<IApiClientProvider>().Todo);
            builder.Services.AddTransient<IEventLogApi>(sp => sp.GetRequiredService<IApiClientProvider>().EventLog);

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
            builder.Services.AddScoped<QuickActivityService>();
            builder.Services.AddScoped<IScanOrchestrator, ScanOrchestrator>();
            builder.Services.AddSingleton<ISharedDataCache, SharedDataCache>();
            builder.Services.AddSingleton<IUserPreferencesService, UserPreferencesService>();
            builder.Services.AddSingleton<IPageTitleService, PageTitleService>();
            builder.Services.Add(new ServiceDescriptor(
                typeof(IThemeService),
                typeof(ThemeService),
                ServiceLifetime.Singleton));

            // Register notification services
#if ANDROID
            builder.Services.AddSingleton<INotificationManagerService, LogMyDay.App.Mobile.Platforms.Android.NotificationManagerService>();
#endif
            builder.Services.AddSingleton<NotificationNavigationService>();
            builder.Services.AddSingleton<ReminderNotificationScheduler>();
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

        // Wire up authentication expiration handling
        // When 401 Unauthorized is detected, automatically clear credentials and redirect to login
        try
        {
            var apiContext = app.Services.GetRequiredService<IApiContext>();
            var authService = app.Services.GetRequiredService<AuthenticationService>();

            apiContext.AuthenticationExpired += () =>
            {
                System.Diagnostics.Debug.WriteLine("🚨 [AUTH EXPIRE] Event fired on thread: " + Environment.CurrentManagedThreadId);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        // Clear the API context (removes username/password from memory)
                        apiContext.Clear();

                        // Clear authentication state and stored preferences
                        authService.HandleAuthenticationExpired();
                    }
                    catch (Exception)
                    {
                        // Even if something fails, try to clear authentication
                        try
                        {
                            authService.SetAuthenticated(false);
                        }
                        catch
                        {
                            // Last resort - ignore errors
                        }
                    }
                });
            };

            System.Diagnostics.Debug.WriteLine("MauiProgram: Authentication expiration handler wired up");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MauiProgram: Error wiring up auth expiration handler: {ex.Message}");
        }

        return app;
    }
}
