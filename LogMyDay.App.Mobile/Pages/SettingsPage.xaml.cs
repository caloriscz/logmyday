using LogMyDay.App.Mobile.ViewModels;

namespace LogMyDay.App.Mobile.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
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
}
