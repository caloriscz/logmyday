using LogMyDay.App.Mobile.Services;
#if ANDROID
using LogMyDay.App.Mobile.Platforms.Android;
#endif

namespace LogMyDay.App.Mobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = "LogMyDay" };
    }

    protected override void OnStart()
    {
        base.OnStart();

        System.Diagnostics.Debug.WriteLine("App.OnStart called");

        // Initialize theme
        InitializeThemeAsync().ConfigureAwait(false);
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        // System notifications continue running in background
    }

    protected override void OnResume()
    {
        base.OnResume();
        // System notifications should continue from where they left off
    }

    private async Task InitializeThemeAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Initializing theme...");
            var serviceProvider = IPlatformApplication.Current?.Services;
            var themeService = serviceProvider?.GetService<IThemeService>();
            
            if (themeService != null)
            {
                await themeService.InitializeAsync();
                System.Diagnostics.Debug.WriteLine("Theme initialized successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("ThemeService not available");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing theme: {ex.Message}");
        }
    }
}
