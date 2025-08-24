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
            builder.Services.AddSingleton<ServerConfigurationService>();
            System.Diagnostics.Debug.WriteLine("MauiProgram: ServerConfigurationService registered");

            // Register app settings
            builder.Services.AddSingleton<AppSettings>(provider =>
            {
                var serverUrl = Preferences.Get("ServerUrl", "https://logmyday.tadata.cz");
                return new AppSettings { WebUrl = serverUrl, DefaultPage = "/" };
            });

            // Register authentication handler
            builder.Services.AddTransient<AuthenticationHeaderHandler>();
            builder.Services.AddRefitClient<IActivityApi>()
                .ConfigureHttpClient(c => 
                {
                    // Use a fixed base address - don't rely on Preferences during DI setup
                    c.BaseAddress = new Uri("https://logmyday.tadata.cz/");
                    c.Timeout = TimeSpan.FromSeconds(30); // 30 second timeout
                    
                    System.Diagnostics.Debug.WriteLine($"========================= [HttpClient] Configured base address: {c.BaseAddress}");
                    System.Diagnostics.Debug.WriteLine($"========================= [HttpClient] Timeout: {c.Timeout}");
                })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
                {
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
                    {
                        // For development - accept all certificates
                        // In production, you should validate certificates properly
                        System.Diagnostics.Debug.WriteLine($"[SSL] Certificate validation: {sslPolicyErrors}");
                        return true;
                    }
                })
                .AddHttpMessageHandler<AuthenticationHeaderHandler>();
            System.Diagnostics.Debug.WriteLine("MauiProgram: Refit API clients registered");

            // Register other services
            builder.Services.AddScoped<ApiService>();
            builder.Services.AddScoped<QuickActivityService>();

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
