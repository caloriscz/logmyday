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
            var username = Preferences.Get("Username", "");
            var password = Preferences.Get("Password", "");
            var hasCredentials = !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password);
            
            System.Diagnostics.Debug.WriteLine($"AuthService: HasCredentials = {hasCredentials}");
            SetAuthenticated(hasCredentials);
            return hasCredentials;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AuthService: Exception in CheckAuthentication: {ex}");
            SetAuthenticated(false);
            return false;
        }
    }

    public async void ClearAuthentication()
    {
        try
        {
            Preferences.Remove("ServerUrl");
            Preferences.Remove("Username");  
            Preferences.Remove("Password");
            SetAuthenticated(false);
            
            // Force navigation to login page immediately
            await Shell.Current.GoToAsync("//login");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error clearing authentication: {ex.Message}");
            SetAuthenticated(false);
            
            // Fallback navigation attempt
            try
            {
                await Shell.Current.GoToAsync("//login");
            }
            catch (Exception navEx)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation fallback error: {navEx.Message}");
            }
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
