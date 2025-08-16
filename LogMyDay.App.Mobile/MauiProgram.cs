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

        // API configuration - simplified
        var apiBaseUrl = "https://logmyday.tadata.cz";
        
        builder.Services.AddTransient<BasicAuthHandler>(provider => 
            new BasicAuthHandler("admin", "secret123"));

        builder.Services.AddRefitClient<IActivityApi>()
            .ConfigureHttpClient(c => c.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<BasicAuthHandler>();

        // Essential services only
        builder.Services.AddSingleton<ApiService>();
        builder.Services.AddSingleton<QuickActivityService>();
        builder.Services.AddSingleton<AuthenticationService>(provider => AuthenticationService.Instance);
        
        // Core ViewModels
        builder.Services.AddTransient<QuickActivitiesViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<AddActivityViewModel>();
        builder.Services.AddTransient<ActivitiesViewModel>();
        builder.Services.AddTransient<TagsViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        
        // Core Pages
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<QuickActivitiesPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<AddActivityPage>();
        builder.Services.AddTransient<ActivitiesPage>();
        builder.Services.AddTransient<TagsPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddSingleton<MainPage>();

        // Register essential routes only
        Routing.RegisterRoute("settings", typeof(SettingsPage));
        Routing.RegisterRoute("addactivity", typeof(AddActivityPage));
        Routing.RegisterRoute("quickactivities", typeof(QuickActivitiesPage));
        Routing.RegisterRoute("login", typeof(LoginPage));

        return builder.Build();
    }
}
