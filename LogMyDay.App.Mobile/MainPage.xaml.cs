using LogMyDay.App.Mobile.Pages;
using LogMyDay.App.Mobile.Services;

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
        }
    }
}
