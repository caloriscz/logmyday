using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using LogMyDay.App.Mobile.Services;

namespace LogMyDay.App.Mobile.ViewModels;

public class LoginViewModel : INotifyPropertyChanged
{
    private readonly ApiService _apiService;
    private readonly AuthenticationService _authService;
    private string _serverUrl = "https://logmyday.tadata.cz";
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _isPasswordHidden = true;
    private bool _isTesting = false;
    private string _errorMessage = string.Empty;
    private bool _hasError = false;

    public LoginViewModel(ApiService apiService)
    {
        _apiService = apiService;
        _authService = AuthenticationService.Instance;
        LoadSavedCredentials();
        
        LoginCommand = new Command(async () => await LoginAsync());
        TogglePasswordVisibilityCommand = new Command(() => IsPasswordHidden = !IsPasswordHidden);
    }

    public string ServerUrl
    {
        get => _serverUrl;
        set
        {
            if (_serverUrl != value)
            {
                _serverUrl = value;
                OnPropertyChanged();
                ClearError();
            }
        }
    }

    public string Username
    {
        get => _username;
        set
        {
            if (_username != value)
            {
                _username = value;
                OnPropertyChanged();
                ClearError();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (_password != value)
            {
                _password = value;
                OnPropertyChanged();
                ClearError();
            }
        }
    }

    public bool IsPasswordHidden
    {
        get => _isPasswordHidden;
        set
        {
            if (_isPasswordHidden != value)
            {
                _isPasswordHidden = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PasswordToggleText));
            }
        }
    }

    public string PasswordToggleText => IsPasswordHidden ? "Show" : "Hide";

    public bool IsTesting
    {
        get => _isTesting;
        set
        {
            if (_isTesting != value)
            {
                _isTesting = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotTesting));
            }
        }
    }

    public bool IsNotTesting => !IsTesting;

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                OnPropertyChanged();
                HasError = !string.IsNullOrEmpty(value);
            }
        }
    }

    public bool HasError
    {
        get => _hasError;
        set
        {
            if (_hasError != value)
            {
                _hasError = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand LoginCommand { get; }
    public ICommand TogglePasswordVisibilityCommand { get; }

    private void LoadSavedCredentials()
    {
        try
        {
            ServerUrl = Preferences.Get("ServerUrl", "https://logmyday.tadata.cz");
            Username = Preferences.Get("Username", "");
            Password = Preferences.Get("Password", "");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading saved credentials: {ex.Message}");
        }
    }

    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter both username and password";
            return;
        }

        IsTesting = true;
        ClearError();

        try
        {
            // Save credentials to preferences
            Preferences.Set("ServerUrl", ServerUrl);
            Preferences.Set("Username", Username);
            Preferences.Set("Password", Password);

            // Test the connection by trying to fetch activities
            await _apiService.GetActivitiesAsync(DateTime.Today);

            // If we get here, login was successful - set authentication state
            _authService.SetAuthenticated(true);

            // Navigate to the main app
            await Shell.Current.GoToAsync("//main/app");
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = "Network error. Please check your connection and server URL.";
            System.Diagnostics.Debug.WriteLine($"Network error during login: {ex.Message}");
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = "Invalid username or password. Please try again.";
            ClearSavedCredentials();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Login failed. Please check your credentials and try again.";
            System.Diagnostics.Debug.WriteLine($"Login error: {ex.Message}");
        }
        finally
        {
            IsTesting = false;
        }
    }

    private void ClearError()
    {
        ErrorMessage = string.Empty;
    }

    private void ClearSavedCredentials()
    {
        try
        {
            Preferences.Remove("Username");
            Preferences.Remove("Password");
            Password = string.Empty;
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
