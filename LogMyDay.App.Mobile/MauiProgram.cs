using LogMyDay.App.Mobile.Services;
using LogMyDay.Shared.Interfaces;
using Refit;
using CommunityToolkit.Maui;
using Microsoft.AspNetCore.Components.WebView.Maui;
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

        System.Diagnostics.Debug.WriteLine("MauiProgram: Basic MAUI configuration completed");

        // Add Blazor WebView
        builder.Services.AddMauiBlazorWebView();
        System.Diagnostics.Debug.WriteLine("MauiProgram: BlazorWebView added");

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
        System.Diagnostics.Debug.WriteLine("MauiProgram: Blazor developer tools and debug logging added");
#endif

        // Register essential services
        try
        {
            System.Diagnostics.Debug.WriteLine("MauiProgram: Starting service registration");

            // Register authentication service
            builder.Services.AddSingleton<AuthenticationService>();
            System.Diagnostics.Debug.WriteLine("MauiProgram: AuthenticationService registered");

            // Register app settings
            builder.Services.AddSingleton<AppSettings>(provider =>
            {
                return new AppSettings { WebUrl = "https://logmyday.tadata.cz", DefaultPage = "/" };
            });
            System.Diagnostics.Debug.WriteLine("MauiProgram: AppSettings registered");

            // Register API services with Refit
            var baseUrl = "https://logmyday.tadata.cz";
            builder.Services.AddRefitClient<IActivityApi>()
                .ConfigureHttpClient(c => c.BaseAddress = new Uri(baseUrl));
            System.Diagnostics.Debug.WriteLine("MauiProgram: Refit API clients registered");

            // Register other services
            builder.Services.AddScoped<ApiService>();
            builder.Services.AddScoped<QuickActivityService>();
            System.Diagnostics.Debug.WriteLine("MauiProgram: Other services registered");

            System.Diagnostics.Debug.WriteLine("MauiProgram: All services registered successfully");
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
