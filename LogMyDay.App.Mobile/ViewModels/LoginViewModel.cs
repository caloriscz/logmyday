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

        System.Diagnostics.Debug.WriteLine($"=== LOGIN ATTEMPT STARTED ===");
        System.Diagnostics.Debug.WriteLine($"Server URL: {ServerUrl}");
        System.Diagnostics.Debug.WriteLine($"Username: '{Username}'");
        System.Diagnostics.Debug.WriteLine($"Password Length: {Password.Length}");

        // Test network connectivity first
        if (!await TestNetworkConnectivity())
        {
            ErrorMessage = "No network connection available. Please check your internet connection.";
            IsTesting = false;
            return;
        }

        try
        {
            // Save credentials to preferences BEFORE making API call
            // This ensures the AuthenticationHeaderHandler can access them
            System.Diagnostics.Debug.WriteLine($"[Login] Saving credentials to preferences...");
            Preferences.Set("ServerUrl", ServerUrl);
            Preferences.Set("Username", Username);
            Preferences.Set("Password", Password);
            System.Diagnostics.Debug.WriteLine($"[Login] Credentials saved successfully");

            System.Diagnostics.Debug.WriteLine($"[Login] Making API test call...");

            // Test the connection by trying to fetch activities
            await _apiService.GetActivitiesAsync(DateTime.Today);

            System.Diagnostics.Debug.WriteLine($"✅ LOGIN SUCCESS - API call completed");

            // If we get here, login was successful - set authentication state
            // The MainLayout will automatically handle navigation to /activities when authentication state changes
            _authService.SetAuthenticated(true);
            
            System.Diagnostics.Debug.WriteLine($"✅ Authentication state set to true - MainLayout will handle navigation");
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ LOGIN FAILED - HTTP Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"HTTP Inner Exception: {ex.InnerException?.Message}");
            
            if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
            {
                ErrorMessage = "Invalid username or password. Please use 'admin' and 'secret123'.";
            }
            else if (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
            {
                ErrorMessage = "Server URL not found. Please check the server address.";
            }
            else if (ex.Message.Contains("timeout") || ex.Message.Contains("timed out"))
            {
                ErrorMessage = "Connection timeout. Please check your network connection.";
            }
            else
            {
                ErrorMessage = $"Network error: {ex.Message}";
            }
            
            ClearSavedCredentials();
        }
        catch (TaskCanceledException ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ LOGIN FAILED - Timeout: {ex.Message}");
            ErrorMessage = "Connection timeout. Please check your network and server URL.";
            ClearSavedCredentials();
        }
        catch (UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"❌ LOGIN FAILED - Unauthorized");
            ErrorMessage = "Invalid username or password. Please use 'admin' and 'secret123'.";
            ClearSavedCredentials();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ LOGIN FAILED - General Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Exception Type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            ErrorMessage = $"Login failed: {ex.Message}. Please check your credentials.";
        }
        finally
        {
            IsTesting = false;
            System.Diagnostics.Debug.WriteLine($"=== LOGIN ATTEMPT COMPLETED ===");
        }
    }

    private async Task<bool> TestNetworkConnectivity()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[Connectivity] Testing network connectivity...");
            
            // Check basic network access
            var connectivity = Connectivity.Current;
            var networkAccess = connectivity.NetworkAccess;
            
            System.Diagnostics.Debug.WriteLine($"[Connectivity] Network access: {networkAccess}");
            
            if (networkAccess != NetworkAccess.Internet)
            {
                System.Diagnostics.Debug.WriteLine($"[Connectivity] ❌ No internet access");
                return false;
            }

            // Test very basic HTTP connectivity - try a simple HEAD request
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            System.Diagnostics.Debug.WriteLine($"[Connectivity] Testing basic HTTP connectivity...");
            
            // Try to reach a simple, reliable endpoint first
            try
            {
                var googleResponse = await httpClient.GetAsync("https://www.google.com");
                System.Diagnostics.Debug.WriteLine($"[Connectivity] Google test: {googleResponse.StatusCode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Connectivity] ❌ Google test failed: {ex.Message}");
                return false;
            }
            
            // Now test our actual server
            System.Diagnostics.Debug.WriteLine($"[Connectivity] Testing connection to: {ServerUrl}");
            try
            {
                var response = await httpClient.GetAsync(ServerUrl);
                System.Diagnostics.Debug.WriteLine($"[Connectivity] Server response status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"[Connectivity] ✅ Server is reachable");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Connectivity] ❌ Server connectivity test failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[Connectivity] Exception type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"[Connectivity] Inner exception: {ex.InnerException?.Message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Connectivity] ❌ Network test failed: {ex.Message}");
            return false;
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
