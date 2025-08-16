using LogMyDay.App.Mobile.Pages;
using LogMyDay.App.Mobile.Services;

namespace LogMyDay.App.Mobile;

public partial class MainPage : Shell
{
    private readonly AuthenticationService _authService;

    public MainPage()
    {
        try
        {
            InitializeComponent();
            _authService = AuthenticationService.Instance;
            
            // Add navigation guard
            this.Navigating += OnNavigating;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainPage constructor error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        // Allow navigation to login page always
        if (e.Target.Location.OriginalString.Contains("login"))
            return;

        // Check authentication for all other routes
        if (!_authService.IsAuthenticated)
        {
            // Cancel the navigation and redirect to login
            e.Cancel();
            
            // Navigate to login page instead
            _ = Task.Run(async () =>
            {
                try
                {
                    await Shell.Current.GoToAsync("//login");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Redirect to login error: {ex.Message}");
                }
            });
        }
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("settings");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Settings navigation error: {ex.Message}");
            await DisplayAlert("Error", $"Unable to open settings: {ex.Message}", "OK");
        }
    }

    private async void OnNewActivityClicked(object sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("addactivity");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Add activity navigation error: {ex.Message}");
            await DisplayAlert("Error", $"Unable to open add activity: {ex.Message}", "OK");
        }
    }
}
