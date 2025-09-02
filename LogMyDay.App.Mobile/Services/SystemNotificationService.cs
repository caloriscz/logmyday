using System.Timers;
using LogMyDay.App.Mobile.Services;
using LogMyDay.Shared.Interfaces;

namespace LogMyDay.App.Mobile.Services;

public interface ISystemNotificationService
{
    void StartMonitoring();
    void StopMonitoring();
    bool IsRunning { get; }
}

public class SystemNotificationService : ISystemNotificationService, IDisposable
{
    private readonly IApiClientProvider _apiClientProvider;
    private readonly INotificationManagerService _notificationService;
    private readonly IApiContext _apiContext;
    private readonly AuthenticationService _authService;
    private System.Timers.Timer? _checkTimer;
    private bool _isRunning;

    public SystemNotificationService(
        IApiClientProvider apiClientProvider,
        INotificationManagerService notificationService,
        IApiContext apiContext,
        AuthenticationService authService)
    {
        _apiClientProvider = apiClientProvider;
        _notificationService = notificationService;
        _apiContext = apiContext;
        _authService = authService;

        // Listen to authentication changes
        _authService.AuthenticationChanged += OnAuthenticationChanged;
    }

    public bool IsRunning => _isRunning;

    private void OnAuthenticationChanged(bool isAuthenticated)
    {
        System.Diagnostics.Debug.WriteLine($"SystemNotificationService: AuthenticationChanged event received - isAuthenticated: {isAuthenticated}");
        
        if (isAuthenticated)
        {
            System.Diagnostics.Debug.WriteLine("SystemNotificationService: User authenticated, starting monitoring");
            StartMonitoring();
            
            // Send a test notification to verify the system works
            _notificationService.SendNotification("LogMyDay", "✅ Monitoring started for unfilled activities");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("SystemNotificationService: User logged out, stopping monitoring");
            StopMonitoring();
        }
    }

    public void StartMonitoring()
    {
        if (_isRunning)
        {
            System.Diagnostics.Debug.WriteLine("SystemNotificationService: Already running, ignoring StartMonitoring call");
            return;
        }

        System.Diagnostics.Debug.WriteLine("SystemNotificationService: Starting monitoring for unfilled required tags");

        // Check immediately after starting
        System.Diagnostics.Debug.WriteLine("SystemNotificationService: Performing immediate check for unfilled tags");
        _ = CheckForUnfilledTags();

        // Set up timer for every 30 seconds (30,000 milliseconds) for testing
        // TODO: Change back to 5 minutes (300,000 milliseconds) for production
        _checkTimer = new System.Timers.Timer(30000);
        _checkTimer.Elapsed += OnTimerElapsed;
        _checkTimer.AutoReset = true;
        _checkTimer.Enabled = true;
        _isRunning = true;

        System.Diagnostics.Debug.WriteLine("SystemNotificationService: Timer started - checking every 30 seconds (testing mode)");
    }

    public void StopMonitoring()
    {
        if (!_isRunning)
        {
            return;
        }

        System.Diagnostics.Debug.WriteLine("SystemNotificationService: Stopping monitoring");

        _checkTimer?.Stop();
        _checkTimer?.Dispose();
        _checkTimer = null;
        _isRunning = false;
    }

    private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        await CheckForUnfilledTags();
    }

    private async Task CheckForUnfilledTags()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("SystemNotificationService: CheckForUnfilledTags started");
            
            // Check if user is authenticated
            if (!_apiContext.IsConfigured)
            {
                System.Diagnostics.Debug.WriteLine("SystemNotificationService: User not authenticated, skipping check");
                return;
            }

            System.Diagnostics.Debug.WriteLine("SystemNotificationService: User authenticated, checking for unfilled required tags");

            var activityApi = _apiClientProvider.Activity;
            var today = DateTime.Today;
            var dateString = today.ToString("yyyy-MM-dd");

            System.Diagnostics.Debug.WriteLine($"SystemNotificationService: Checking date: {dateString}");

            var unfilledTags = await activityApi.GetRequiredDailyTagsNotFilledForDate(dateString);

            System.Diagnostics.Debug.WriteLine($"SystemNotificationService: API call completed, unfilled tags count: {unfilledTags?.Count ?? 0}");

            if (unfilledTags != null && unfilledTags.Count > 0)
            {
                var message = unfilledTags.Count == 1
                    ? "You have 1 unfilled required activity for today"
                    : $"You have {unfilledTags.Count} unfilled required activities for today";

                System.Diagnostics.Debug.WriteLine($"SystemNotificationService: Found {unfilledTags.Count} unfilled required tags, sending notification");

                _notificationService.SendNotification("LogMyDay", message);
                System.Diagnostics.Debug.WriteLine($"SystemNotificationService: Notification sent: {message}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("SystemNotificationService: No unfilled required tags found");
                
                // Send a debug notification to verify the system is working
                _notificationService.SendNotification("LogMyDay Debug", "✅ Check completed - no unfilled activities");
            }
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            System.Diagnostics.Debug.WriteLine("SystemNotificationService: Authentication failed, will retry on next check");
            // Don't stop the service, just skip this check - authentication might recover
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SystemNotificationService: Error checking for unfilled tags: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"SystemNotificationService: Exception details: {ex}");
        }
    }

    public void Dispose()
    {
        // Unhook from authentication events
        _authService.AuthenticationChanged -= OnAuthenticationChanged;
        
        StopMonitoring();
    }
}
