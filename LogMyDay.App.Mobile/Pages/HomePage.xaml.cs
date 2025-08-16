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
            _currentUrl = value;
            OnPropertyChanged();
        }
    }

    public HomePage(AppSettings appSettings)
    {
        try
        {
            InitializeComponent();
            _appSettings = appSettings ?? new AppSettings { WebUrl = "https://logmyday.tadata.cz", DefaultPage = "/" };
            CurrentUrl = _appSettings.FullUrl;
            BindingContext = this;
        }
        catch (Exception ex)
        {
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
            if (!string.IsNullOrEmpty(CurrentUrl))
            {
                webView.Source = CurrentUrl;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HomePage OnAppearing error: {ex.Message}");
            webView.Source = "https://logmyday.tadata.cz";
        }
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected new virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
