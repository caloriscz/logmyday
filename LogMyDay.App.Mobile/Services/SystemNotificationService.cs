using System.Diagnostics;
using System.Linq;
using System.Timers;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using LogMyDay.Shared.Notifications;
using Microsoft.Extensions.Logging;
using Refit;

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
    private readonly INotificationStateStore _stateStore;
    private readonly ILogger<SystemNotificationService> _logger;
    private System.Timers.Timer? _checkTimer;
    private bool _isRunning;
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private readonly List<NotificationResponse> _cachedNotifications = new();
    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private bool _isDisposed;
    private const string LogPrefix = "SystemNotificationService";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(15);

    public SystemNotificationService(
        IApiClientProvider apiClientProvider,
        INotificationManagerService notificationService,
        IApiContext apiContext,
        AuthenticationService authService,
        INotificationStateStore stateStore,
        ILogger<SystemNotificationService> logger)
    {
        _apiClientProvider = apiClientProvider;
        _notificationService = notificationService;
        _apiContext = apiContext;
        _authService = authService;
        _stateStore = stateStore;
        _logger = logger;

        // Listen to authentication changes
        _authService.AuthenticationChanged += OnAuthenticationChanged;

        _logger.LogDebug("SystemNotificationService constructed; awaiting authentication events");
        WriteDebug("Constructed and subscribed to authentication events");
    }

    public bool IsRunning => _isRunning;

    private void OnAuthenticationChanged(bool isAuthenticated)
    {
        _logger.LogInformation("Authentication state changed. Authenticated: {IsAuthenticated}", isAuthenticated);
        WriteDebug($"AuthenticationChanged received (isAuthenticated={isAuthenticated})");

        if (isAuthenticated)
        {
            StartMonitoring();
        }
        else
        {
            StopMonitoring();
        }
    }

    public void StartMonitoring()
    {
        if (_isRunning)
        {
            return;
        }

        _logger.LogInformation("Starting notification monitoring loop");
        WriteDebug("Starting notification monitoring loop");

        _ = CheckNotificationsAsync(forceRefresh: true);

        _checkTimer = new System.Timers.Timer(CheckInterval.TotalMilliseconds);
        _checkTimer.Elapsed += OnTimerElapsed;
        _checkTimer.AutoReset = true;
        _checkTimer.Enabled = true;
        _isRunning = true;
        _logger.LogInformation("Notification timer started; interval {Interval}", CheckInterval);
        WriteDebug($"Notification timer started; interval {CheckInterval}");
    }

    public void StopMonitoring()
    {
        if (!_isRunning)
        {
            return;
        }

        _logger.LogInformation("Stopping notification monitoring loop");
        WriteDebug("Stopping notification monitoring loop");

        _checkTimer?.Stop();
        _checkTimer?.Dispose();
        _checkTimer = null;
        _isRunning = false;

        _cachedNotifications.Clear();
        _lastRefreshUtc = DateTime.MinValue;
        _stateStore.ClearAll();
    }

    private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        WriteDebug("Timer tick received from System.Timers.Timer");
        await CheckNotificationsAsync();
    }

    private async Task CheckNotificationsAsync(bool forceRefresh = false)
    {
        WriteDebug("Timer elapsed – triggering notification check");

        if (_isDisposed)
        {
            return;
        }

        if (!_apiContext.IsConfigured)
        {
            _logger.LogDebug("API context not configured; skipping notification poll");
            WriteDebug("API context not configured; skipping notification poll");
            return;
        }

        if (!await _checkLock.WaitAsync(0))
        {
            _logger.LogDebug("Previous notification poll still in progress; skipping");
            WriteDebug("Previous poll still in progress; skipping");
            return;
        }

        try
        {
            _logger.LogDebug("Running notification poll (forceRefresh={ForceRefresh})", forceRefresh);
            WriteDebug($"Running notification poll (forceRefresh={forceRefresh})");

            await RefreshNotificationsAsync(forceRefresh);

            if (_cachedNotifications.Count == 0)
            {
                _logger.LogDebug("No notifications configured; skipping poll");
                WriteDebug("No notifications configured; skipping poll");
                return;
            }

            var pruneThreshold = DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
            _stateStore.PruneOlderThan(pruneThreshold);

            var now = DateTime.Now;
            foreach (var notification in _cachedNotifications)
            {
                if (!notification.IsActive)
                {
                    continue;
                }

                ProcessNotification(notification, now);
            }
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning(ex, "Notification poll unauthorized; will retry later");
            WriteDebug($"Notification poll unauthorized ({ex.StatusCode}); will retry later");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during notification poll");
            WriteDebug($"Unexpected error during notification poll - {ex.Message}");
        }
        finally
        {
            _checkLock.Release();
            WriteDebug("Notification poll completed");
        }
    }

    private async Task RefreshNotificationsAsync(bool forceRefresh)
    {
        if (!forceRefresh && DateTime.UtcNow - _lastRefreshUtc < RefreshInterval)
        {
            _logger.LogDebug("Skipping notification refresh – cache still fresh (next refresh in {Remaining} seconds)",
                (RefreshInterval - (DateTime.UtcNow - _lastRefreshUtc)).TotalSeconds);
            WriteDebug($"Skipping notification refresh – cache interval not reached; cached notifications = {_cachedNotifications.Count}");
            return;
        }

        var activityApi = _apiClientProvider.Activity;
        var notifications = await activityApi.GetNotifications();

        _cachedNotifications.Clear();
        if (notifications != null)
        {
            _cachedNotifications.AddRange(notifications);
        }

        _lastRefreshUtc = DateTime.UtcNow;
        _stateStore.RemoveObsoleteNotifications(_cachedNotifications.Select(n => n.Id));
        _logger.LogInformation("Loaded {Count} notifications for scheduling", _cachedNotifications.Count);
        WriteDebug($"Loaded {_cachedNotifications.Count} notifications for scheduling");
    }

    private void ProcessNotification(NotificationResponse notification, DateTime now)
    {
        if (!notification.IsActive)
        {
            WriteDebug($"Notification {notification.Id} ({notification.TagName ?? "(no tag name)"}) inactive; skipping");
            return;
        }

        WriteDebug($"Evaluating notification {notification.Id} ({notification.TagName ?? "(no tag name)"}) at {now:O}");

        var windows = NotificationScheduleCalculator.BuildWindows(notification, now);
        foreach (var window in windows)
        {
            if (!window.Contains(now))
            {
                WriteDebug($"Window {window.BaseDate}: current time {now:HH:mm:ss} outside range {window.Start:HH:mm:ss} - {window.End?.ToString("HH:mm:ss") ?? "(no end)"}; skipped");
                continue;
            }

            var state = _stateStore.GetState(notification.Id, window.BaseDate);
            WriteDebug($"Window {window.BaseDate}: total occurrences={window.TotalOccurrences}, already sent={state.NudgesSent}");

            var occurrencesToSend = CalculateOccurrencesToSend(window, state, now);
            if (occurrencesToSend <= 0)
            {
                var nextIndex = Math.Min(state.NudgesSent, window.TotalOccurrences - 1);
                var nextTime = nextIndex >= 0 ? window.GetOccurrenceTime(nextIndex) : (DateTime?)null;
                WriteDebug($"Window {window.BaseDate}: no notifications to send (next scheduled at {nextTime:O})");
                continue;
            }

            WriteDebug($"Dispatching {occurrencesToSend} occurrence(s) for notification {notification.Id} on {window.BaseDate}");

            for (int i = 0; i < occurrencesToSend; i++)
            {
                SendNotification(notification);
                state = state.IncrementNudges(DateTime.UtcNow);
            }

            _stateStore.SaveState(notification.Id, state);
        }
    }

    private static int CalculateOccurrencesToSend(NotificationWindow window, NotificationStateSnapshot state, DateTime now)
    {
        var occurrencesSent = state.NudgesSent;
        if (occurrencesSent >= window.TotalOccurrences)
        {
            return 0;
        }

        var occurrences = 0;

        while (occurrencesSent < window.TotalOccurrences)
        {
            var scheduledTime = window.GetOccurrenceTime(occurrencesSent);
            if (window.End.HasValue && scheduledTime > window.End.Value)
            {
                break;
            }

            if (scheduledTime > now)
            {
                break;
            }

            occurrences++;
            occurrencesSent++;
        }

        return occurrences;
    }

    private void SendNotification(NotificationResponse notification)
    {
        var message = string.IsNullOrWhiteSpace(notification.NotificationText)
            ? NotificationScheduleCalculator.BuildDefaultMessage(notification)
            : notification.NotificationText!;

        _notificationService.SendNotification("LogMyDay", message);
        _logger.LogDebug("Notification dispatched for tag {TagId}", notification.TagId);
        WriteDebug($"Notification dispatched for tag {notification.TagId}");
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        // Unhook from authentication events
        _authService.AuthenticationChanged -= OnAuthenticationChanged;
        StopMonitoring();
        _checkLock.Dispose();
        _isDisposed = true;

        _logger.LogDebug("SystemNotificationService disposed");
        WriteDebug("Disposed and detached from authentication events");
    }

    private void WriteDebug(string message)
    {
        Debug.WriteLine($"{LogPrefix}: {message}");
    }
}
