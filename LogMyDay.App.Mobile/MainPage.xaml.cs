using LogMyDay.App.Mobile.Pages;

namespace LogMyDay.App.Mobile;

public partial class MainPage : Shell
{
    public MainPage()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainPage constructor error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
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
            // Show error to user
            await DisplayAlert("Error", $"Unable to open settings: {ex.Message}", "OK");
        }
    }
}
