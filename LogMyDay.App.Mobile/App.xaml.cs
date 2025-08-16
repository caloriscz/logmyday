using LogMyDay.App.Mobile.Pages;
using LogMyDay.App.Mobile.Services;

namespace LogMyDay.App.Mobile;

public partial class App : Application
{
    private readonly MainPage _mainPage;
    private readonly AuthenticationService _authService;

    public App(MainPage mainPage)
    {
        InitializeComponent();
        _mainPage = mainPage;
        _authService = AuthenticationService.Instance;
        
        // Register routes
        Routing.RegisterRoute("settings", typeof(SettingsPage));
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_mainPage)
        {
            Title = "LogMyDay Mobile"
        };
        
        // Check authentication and navigate appropriately
        _ = Task.Run(async () =>
        {
            await Task.Delay(500); // Give the app more time to initialize
            
            try
            {
                var isAuthenticated = _authService.CheckAuthentication();
                
                if (!isAuthenticated)
                {
                    // No credentials saved, go to login
                    await Shell.Current.GoToAsync("//login");
                }
                else
                {
                    // Credentials exist, set auth state and go to main app
                    _authService.SetAuthenticated(true);
                    await Shell.Current.GoToAsync("//main/app");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Initial navigation error: {ex.Message}");
                // Fallback to login page
                try
                {
                    await Shell.Current.GoToAsync("//login");
                }
                catch (Exception navEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Fallback navigation error: {navEx.Message}");
                }
            }
        });
        
        return window;
    }
}
