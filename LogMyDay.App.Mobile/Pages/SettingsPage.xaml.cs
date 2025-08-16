using LogMyDay.App.Mobile.ViewModels;
using LogMyDay.App.Mobile.Services;

namespace LogMyDay.App.Mobile.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;
    private readonly AuthenticationService _authService;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _authService = AuthenticationService.Instance;
        BindingContext = _viewModel;
    }

    private async void OnDoneClicked(object sender, EventArgs e)
    {
        try
        {
            // Use absolute navigation to return to the home tab
            await Shell.Current.GoToAsync("//home");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation back error: {ex.Message}");
            try
            {
                // Alternative: Try relative navigation
                await Shell.Current.GoToAsync("..");
            }
            catch
            {
                // Last resort: Display error
                await DisplayAlert("Navigation Error", "Unable to navigate back. Please use the back gesture or hardware back button.", "OK");
            }
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        try
        {
            var confirmed = await DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
            if (confirmed)
            {
                // Clear stored credentials using the viewmodel
                await _viewModel.ClearCredentialsAsync();
                
                // Clear authentication and navigate to login (this will trigger navigation)
                _authService.ClearAuthentication();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Logout error: {ex.Message}");
            await DisplayAlert("Error", "An error occurred during logout.", "OK");
        }
    }
}
