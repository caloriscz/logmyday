using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Storage;

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

    public void ClearAuthentication()
    {
        try
        {
            Preferences.Remove("ServerUrl");
            Preferences.Remove("Username");
            // Password is never persisted - stored only in memory via ApiContext
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

    public void HandleAuthenticationExpired()
    {
        System.Diagnostics.Debug.WriteLine("🚨 AuthenticationService: Handling authentication expiration");
        System.Diagnostics.Debug.WriteLine("   This usually means password was changed or session is invalid");
        
        // Clear authentication and stored credentials
        ClearAuthentication();
        
        System.Diagnostics.Debug.WriteLine("🚨 Authentication expired - user will be redirected to login");
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
