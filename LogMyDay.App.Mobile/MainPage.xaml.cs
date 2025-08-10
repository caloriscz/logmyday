using LogMyDay.App.Mobile.Services;
using System.ComponentModel;

namespace LogMyDay.App.Mobile;

public partial class MainPage : ContentPage, INotifyPropertyChanged
{
    private readonly AppSettings _appSettings;
    private string _currentUrl = string.Empty;

    public string CurrentUrl
    {
        get => _currentUrl;
        set
        {
            if (_currentUrl != value)
            {
                _currentUrl = value;
                OnPropertyChanged();
            }
        }
    }

    public MainPage() : this(null)
    {
    }

    public MainPage(AppSettings? appSettings)
    {
        try
        {
            InitializeComponent();
            _appSettings = appSettings ?? new AppSettings { WebUrl = "https://logmyday.tadata.cz", DefaultPage = "/" };
            
            // Set the initial URL from app settings
            CurrentUrl = _appSettings.FullUrl;
            
            BindingContext = this;
        }
        catch (Exception ex)
        {
            // Log error and set defaults
            System.Diagnostics.Debug.WriteLine($"MainPage constructor error: {ex.Message}");
            _appSettings = new AppSettings { WebUrl = "https://logmyday.tadata.cz", DefaultPage = "/" };
            CurrentUrl = _appSettings.FullUrl;
            BindingContext = this;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        try
        {
            // Navigate to the initial URL
            if (!string.IsNullOrEmpty(CurrentUrl))
            {
                webView.Source = CurrentUrl;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnAppearing error: {ex.Message}");
            // Try to load a fallback URL
            webView.Source = "https://logmyday.tadata.cz";
        }
    }

    private void OnGoButtonClicked(object sender, EventArgs e)
    {
        var url = urlEntry.Text?.Trim();
        if (!string.IsNullOrEmpty(url))
        {
            // Add https:// if no protocol is specified
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }
            
            CurrentUrl = url;
            webView.Source = url;
        }
    }
}
