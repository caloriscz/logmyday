using LogMyDay.App.Mobile.Services;

namespace LogMyDay.App.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Configure app settings
        var appSettings = new AppSettings();
#if DEBUG
        appSettings.WebUrl = "http://localhost:5000";
#else
        appSettings.WebUrl = "https://logmyday.tadata.cz";
#endif
        appSettings.DefaultPage = "/";
        
        builder.Services.AddSingleton(appSettings);

        // Add services
        builder.Services.AddSingleton<ApiService>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Services.AddLogging(logging =>
        {
            logging.AddDebug();
        });
#endif

        return builder.Build();
    }
}
