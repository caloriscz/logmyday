using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace LogMyDay.App.Mobile.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private string _serverUrl = "https://logmyday.tadata.cz";
    private string _username = "";
    private string _password = "";

    public string ServerUrl
    {
        get => _serverUrl;
        set
        {
            _serverUrl = value;
            OnPropertyChanged();
        }
    }

    public string Username
    {
        get => _username;
        set
        {
            _username = value;
            OnPropertyChanged();
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            _password = value;
            OnPropertyChanged();
        }
    }

    public ICommand SaveSettingsCommand { get; }

    public SettingsViewModel()
    {
        SaveSettingsCommand = new Command(async () => await SaveSettings());
        LoadSettings();
    }

    private void LoadSettings()
    {
        // Load settings from preferences
        ServerUrl = Preferences.Get("server_url", "https://logmyday.tadata.cz");
        Username = Preferences.Get("username", "");
        Password = Preferences.Get("password", "");
    }

    private async Task SaveSettings()
    {
        try
        {
            // Save settings to preferences
            Preferences.Set("server_url", ServerUrl);
            Preferences.Set("username", Username);
            Preferences.Set("password", Password);

            // Show success message
            var app = Application.Current;
            var page = app?.Windows?.FirstOrDefault()?.Page;
            if (page != null)
            {
                await page.DisplayAlert("Settings Saved", "Your settings have been saved successfully!", "OK");
            }
        }
        catch (Exception ex)
        {
            // Show error message
            var app = Application.Current;
            var page = app?.Windows?.FirstOrDefault()?.Page;
            if (page != null)
            {
                await page.DisplayAlert("Error", $"Failed to save settings: {ex.Message}", "OK");
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
