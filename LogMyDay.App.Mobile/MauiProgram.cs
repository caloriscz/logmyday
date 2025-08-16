using LogMyDay.App.Mobile.Pages;
using LogMyDay.App.Mobile.Services;
using LogMyDay.App.Mobile.ViewModels;
using LogMyDay.Shared.Interfaces;
using Refit;
using CommunityToolkit.Maui;

namespace LogMyDay.App.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Configure app settings
        var appSettings = new AppSettings
        {
            WebUrl = "https://logmyday.tadata.cz",
            DefaultPage = "/"
        };

        builder.Services.AddSingleton(appSettings);

        // API configuration
        var apiBaseUrl = "https://logmyday.tadata.cz";
        var apiUsername = "admin";
        var apiPassword = "secret123";

        // Add authentication handler with proper credentials
        builder.Services.AddTransient<BasicAuthHandler>(provider => 
            new BasicAuthHandler(apiUsername, apiPassword));

        // Configure Refit client for API
        builder.Services.AddRefitClient<IActivityApi>()
            .ConfigureHttpClient(c =>
            {
                c.BaseAddress = new Uri(apiBaseUrl);
            })
            .AddHttpMessageHandler<BasicAuthHandler>();

        // Add services
        builder.Services.AddSingleton<ApiService>();
        builder.Services.AddSingleton<QuickActivityService>();
        
        // Add view models
        builder.Services.AddTransient<QuickActivitiesViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<TagsViewModel>();
        builder.Services.AddTransient<AddActivityViewModel>();
        builder.Services.AddTransient<ActivitiesViewModel>();
        
        // Add pages
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<QuickActivitiesPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<TagsPage>();
        builder.Services.AddTransient<AddActivityPage>();
        builder.Services.AddTransient<ActivitiesPage>();
        builder.Services.AddSingleton<MainPage>();

        // Register routes
        Routing.RegisterRoute("settings", typeof(SettingsPage));
        Routing.RegisterRoute("addactivity", typeof(AddActivityPage));

        return builder.Build();
    }
}
