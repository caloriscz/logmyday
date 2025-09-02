using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LogMyDay.App.Mobile.Services;

public class AuthenticationService : INotifyPropertyChanged
{
    private static AuthenticationService? _instance;
    private bool _isAuthenticated = false;

    public static AuthenticationService Instance => _instance ??= new AuthenticationService();

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        private set
        {
            if (_isAuthenticated != value)
            {
                _isAuthenticated = value;
                OnPropertyChanged();
                AuthenticationChanged?.Invoke(value);
            }
        }
    }

    public event Action<bool>? AuthenticationChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetAuthenticated(bool isAuthenticated)
    {
        IsAuthenticated = isAuthenticated;
    }

    public bool CheckAuthentication()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("AuthService: CheckAuthentication started");
            
            // IMPORTANT: Just having stored credentials doesn't mean user is authenticated
            // Authentication should be explicitly set only after successful login
            // Don't auto-authenticate based on stored credentials
            
            System.Diagnostics.Debug.WriteLine($"AuthService: Current auth state = {_isAuthenticated}");
            return _isAuthenticated;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AuthService: Exception in CheckAuthentication: {ex}");
            SetAuthenticated(false);
            return false;
        }
    }

    public bool HasStoredCredentials()
    {
        try
        {
            var username = Preferences.Get("Username", "");
            var serverUrl = Preferences.Get("ServerUrl", "");
            // Note: Password is not persisted for security reasons
            return !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(serverUrl);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TryAutoReAuthenticate(IApiContext apiContext, IApiClientProvider apiProvider)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("AuthService: TryAutoReAuthenticate started");

            if (!HasStoredCredentials())
            {
                System.Diagnostics.Debug.WriteLine("AuthService: No stored credentials available");
                return false;
            }

            var storedServerUrl = Preferences.Get("ServerUrl", "");
            var storedUsername = Preferences.Get("Username", "");

            if (!Uri.TryCreate(storedServerUrl, UriKind.Absolute, out var serverUri))
            {
                System.Diagnostics.Debug.WriteLine("AuthService: Invalid stored server URL");
                return false;
            }

            // Check if current context has valid credentials for the stored username
            if (apiContext.Username == storedUsername && !string.IsNullOrEmpty(apiContext.Password))
            {
                // Try to make an API call to verify the credentials are still valid
                var activityApi = apiProvider.Activity;
                var tags = await activityApi.GetTags();

                // If we get here, credentials are still valid
                System.Diagnostics.Debug.WriteLine("AuthService: Auto re-authentication successful");
                SetAuthenticated(true);
                return true;
            }

            System.Diagnostics.Debug.WriteLine("AuthService: Context credentials don't match stored username or missing password");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AuthService: Auto re-authentication failed: {ex.Message}");
            return false;
        }
    }

    public void ClearAuthentication()
    {
        try
        {
            Preferences.Remove("ServerUrl");
            Preferences.Remove("Username");  
            Preferences.Remove("Password");
            SetAuthenticated(false);
            
            // MainLayout will automatically handle navigation to /login when authentication state changes
            System.Diagnostics.Debug.WriteLine("Authentication cleared - MainLayout will handle navigation to login");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing authentication: {ex.Message}");
            SetAuthenticated(false);
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
