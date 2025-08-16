using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Storage;
using LogMyDay.App.Mobile.Services;

namespace LogMyDay.App.Mobile.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ApiService _apiService;
    private string _serverUrl = "https://logmyday.tadata.cz";
    private string _username = "";
    private string _password = "";
    private bool _isPasswordHidden = true;
    private bool _showConnectionStatus = false;
    private string _connectionStatusMessage = "";
    private Color _connectionStatusColor = Colors.Gray;
    private bool _isTesting = false;
    private bool _isSaving = false;

    public SettingsViewModel(ApiService apiService)
    {
        _apiService = apiService;
        
        try
        {
            SaveSettingsCommand = new Command(async () => await SaveSettings());
            TestConnectionCommand = new Command(async () => await TestConnection());
            TogglePasswordVisibilityCommand = new Command(TogglePasswordVisibility);
            LoadSettings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsViewModel constructor error: {ex.Message}");
            // Initialize with defaults if there's an error
            SaveSettingsCommand = new Command(async () => await SaveSettings());
            TestConnectionCommand = new Command(async () => await TestConnection());
            TogglePasswordVisibilityCommand = new Command(TogglePasswordVisibility);
            ServerUrl = "https://logmyday.tadata.cz";
            Username = "";
            Password = "";
        }
    }

    public string ServerUrl
    {
        get => _serverUrl;
        set
        {
            _serverUrl = value;
            OnPropertyChanged();
            HideConnectionStatus();
        }
    }

    public string Username
    {
        get => _username;
        set
        {
            _username = value;
            OnPropertyChanged();
            HideConnectionStatus();
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            _password = value;
            OnPropertyChanged();
            HideConnectionStatus();
        }
    }

    public bool IsPasswordHidden
    {
        get => _isPasswordHidden;
        set
        {
            _isPasswordHidden = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PasswordToggleText));
        }
    }

    public string PasswordToggleText => IsPasswordHidden ? "Show" : "Hide";

    public bool ShowConnectionStatus
    {
        get => _showConnectionStatus;
        set
        {
            _showConnectionStatus = value;
            OnPropertyChanged();
        }
    }

    public string ConnectionStatusMessage
    {
        get => _connectionStatusMessage;
        set
        {
            _connectionStatusMessage = value;
            OnPropertyChanged();
        }
    }

    public Color ConnectionStatusColor
    {
        get => _connectionStatusColor;
        set
        {
            _connectionStatusColor = value;
            OnPropertyChanged();
        }
    }

    public bool IsTesting
    {
        get => _isTesting;
        set
        {
            _isTesting = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotTesting));
        }
    }

    public bool IsNotTesting => !IsTesting;

    public bool IsSaving
    {
        get => _isSaving;
        set
        {
            _isSaving = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotSaving));
        }
    }

    public bool IsNotSaving => !IsSaving;

    public ICommand SaveSettingsCommand { get; private set; }
    public ICommand TestConnectionCommand { get; private set; }
    public ICommand TogglePasswordVisibilityCommand { get; private set; }

    private void LoadSettings()
    {
        try
        {
            // Load settings from preferences
            ServerUrl = Preferences.Get("server_url", "https://logmyday.tadata.cz");
            Username = Preferences.Get("username", "admin");
            Password = Preferences.Get("password", "secret123");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            // Use defaults if loading fails
            ServerUrl = "https://logmyday.tadata.cz";
            Username = "admin";
            Password = "secret123";
        }
    }

    private async Task SaveSettings()
    {
        IsSaving = true;
        
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
                await page.DisplayAlert("✅ Success", "Your settings have been saved successfully!", "OK");
            }
            
            ShowConnectionStatus = false; // Hide status after saving
        }
        catch (Exception ex)
        {
            // Show error message
            var app = Application.Current;
            var page = app?.Windows?.FirstOrDefault()?.Page;
            if (page != null)
            {
                await page.DisplayAlert("❌ Error", $"Failed to save settings: {ex.Message}", "OK");
            }
        }
        finally
        {
            IsSaving = false;
        }
    }

    private async Task TestConnection()
    {
        IsTesting = true;
        ShowConnectionStatus = true;
        ConnectionStatusMessage = "Testing connection...";
        ConnectionStatusColor = Colors.Orange;

        try
        {
            // TODO: Test connection with provided credentials
            // For now, simulate a test
            await Task.Delay(2000);

            var success = await _apiService.TestApiConnectionAsync();
            
            if (success)
            {
                ConnectionStatusMessage = "✅ Connection successful!";
                ConnectionStatusColor = Colors.Green;
            }
            else
            {
                ConnectionStatusMessage = $"❌ Connection failed: {_apiService.LastError}";
                ConnectionStatusColor = Colors.Red;
            }
        }
        catch (Exception ex)
        {
            ConnectionStatusMessage = $"❌ Connection error: {ex.Message}";
            ConnectionStatusColor = Colors.Red;
            System.Diagnostics.Debug.WriteLine($"Connection test error: {ex.Message}");
        }
        finally
        {
            IsTesting = false;
        }
    }

    private void TogglePasswordVisibility()
    {
        IsPasswordHidden = !IsPasswordHidden;
    }

    private void HideConnectionStatus()
    {
        ShowConnectionStatus = false;
    }

    public async Task ClearCredentialsAsync()
    {
        try
        {
            // Clear stored settings
            Preferences.Remove("ServerUrl");
            Preferences.Remove("Username");  
            Preferences.Remove("Password");
            
            // Reset to defaults
            ServerUrl = "https://logmyday.tadata.cz";
            Username = "";
            Password = "";
            
            System.Diagnostics.Debug.WriteLine("Credentials cleared successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing credentials: {ex.Message}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
