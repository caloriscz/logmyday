namespace LogMyDay.App.Mobile;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        System.Diagnostics.Debug.WriteLine("MainPage: Constructor entered");
        try
        {
            System.Diagnostics.Debug.WriteLine("MainPage: Initializing component...");
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("MainPage: InitializeComponent completed");
            
            StatusLabel.Text = "MAUI loaded - Initializing Blazor WebView...";
            System.Diagnostics.Debug.WriteLine("MainPage: Status label updated");
            
            // Wire up WebView events for better debugging
            blazorWebView.BlazorWebViewInitialized += OnBlazorWebViewInitialized;
            blazorWebView.UrlLoading += OnUrlLoading;
            
            System.Diagnostics.Debug.WriteLine("MainPage: WebView events wired up");
            
            // Start monitoring timer
            StartBlazorMonitoring();
            
            System.Diagnostics.Debug.WriteLine("MainPage: Constructor completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainPage constructor error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"MainPage stack trace: {ex.StackTrace}");
            
            if (StatusLabel != null)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
            }
            throw;
        }
    }
    
    private void OnBlazorWebViewInitialized(object? sender, Microsoft.AspNetCore.Components.WebView.BlazorWebViewInitializedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("MainPage: BlazorWebView initialized successfully!");
        StatusLabel.Text = "WebView initialized - Loading Blazor...";
        
        System.Diagnostics.Debug.WriteLine($"MainPage: WebView type: {e.WebView.GetType().Name}");
    }
    
    private void OnUrlLoading(object? sender, Microsoft.AspNetCore.Components.WebView.UrlLoadingEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"MainPage: URL loading: {e.Url}");
        StatusLabel.Text = $"Loading: {e.Url.Scheme}://{e.Url.Host}";
    }
    
    private async void StartBlazorMonitoring()
    {
        //System.Diagnostics.Debug.WriteLine("MainPage: Starting Blazor monitoring");
        
        //// Check after 3 seconds
        //await Task.Delay(3000);
        //StatusLabel.Text = "Waiting for Blazor components...";
        //System.Diagnostics.Debug.WriteLine("MainPage: 3 seconds - waiting for Blazor");
        
        //// Check after 8 seconds
        //await Task.Delay(5000);
        //StatusLabel.Text = "Blazor loading is taking longer than expected...";
        //System.Diagnostics.Debug.WriteLine("MainPage: 8 seconds - Blazor taking longer");
        
        //// Check after 15 seconds
        //await Task.Delay(7000);
        //StatusLabel.Text = "⚠️ Blazor WebView may have a communication issue";
        //System.Diagnostics.Debug.WriteLine("MainPage: 15 seconds - potential WebView communication issue");
    }
}
