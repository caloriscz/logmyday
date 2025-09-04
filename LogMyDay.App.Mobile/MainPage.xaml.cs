using LogMyDay.App.Mobile.Services;

namespace LogMyDay.App.Mobile;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            // Notify the current Blazor page to refresh its data
            RefreshService.RequestRefresh();
        }
        catch (Exception ex)
        {
            // Log the error but don't crash the app
            System.Diagnostics.Debug.WriteLine($"Error during refresh: {ex.Message}");
        }
        finally
        {
            // Stop the refresh animation after a short delay to allow Blazor to respond
            Task.Delay(1000).ContinueWith(_ => 
            {
                Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() => 
                {
                    refreshView.IsRefreshing = false;
                });
            });
        }
    }
}
