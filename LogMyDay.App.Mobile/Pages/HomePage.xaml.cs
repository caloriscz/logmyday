using LogMyDay.App.Mobile.Services;
using System.ComponentModel;

namespace LogMyDay.App.Mobile.Pages;

public partial class HomePage : ContentPage, INotifyPropertyChanged
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

    public HomePage(AppSettings appSettings)
    {
        try
        {
            InitializeComponent();
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            
            // Set the initial URL from app settings
            CurrentUrl = _appSettings.FullUrl;
            
            BindingContext = this;
        }
        catch (Exception ex)
        {
            // Log error and set defaults
            System.Diagnostics.Debug.WriteLine($"HomePage constructor error: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"HomePage OnAppearing error: {ex.Message}");
            // Try to load a fallback URL
            webView.Source = "https://logmyday.tadata.cz";
        }
    }
}
