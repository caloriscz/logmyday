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
        
        // Initialize system notification service 
        try
        {
            System.Diagnostics.Debug.WriteLine("Attempting to get SystemNotificationService from DI");
            var serviceProvider = IPlatformApplication.Current?.Services;
            System.Diagnostics.Debug.WriteLine($"ServiceProvider available: {serviceProvider != null}");
            
            // Force early construction of SystemNotificationService to ensure event subscription
            var systemNotificationService = serviceProvider?.GetService<ISystemNotificationService>();
            System.Diagnostics.Debug.WriteLine($"SystemNotificationService from DI: {systemNotificationService != null}");
            
            // Also force construction of AuthenticationService to ensure it's ready
            var authService = serviceProvider?.GetService<AuthenticationService>();
            System.Diagnostics.Debug.WriteLine($"AuthenticationService from DI: {authService != null}");
            
            if (systemNotificationService != null)
            {
                System.Diagnostics.Debug.WriteLine("SystemNotificationService initialized - will start monitoring after authentication");
                System.Diagnostics.Debug.WriteLine($"SystemNotificationService is running: {systemNotificationService.IsRunning}");
                
                // Note: The service will automatically start monitoring when user authenticates
                // via the AuthenticationService.AuthenticationChanged event
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("SystemNotificationService not found in DI container");
            }

            // Also get the platform notification service to test basic notifications
            var notificationService = serviceProvider?.GetService<NotificationService>();
#if DEBUG
            if (notificationService != null)
            {
                System.Diagnostics.Debug.WriteLine("Testing basic notification service");
                notificationService.SendNotification("LogMyDay", "🚀 App started - notification system active");
            }
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing SystemNotificationService: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Exception details: {ex}");
        }
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
