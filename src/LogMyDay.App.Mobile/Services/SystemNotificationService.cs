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
        ILogger<SystemNotificationService> logger)
    {
        _apiClientProvider = apiClientProvider;
        _notificationService = notificationService;
        _apiContext = apiContext;
        _authService = authService;
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

        // Delay the initial check to avoid competing with page data loading for HTTP connections
        _ = Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ => CheckNotificationsAsync(forceRefresh: true));

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

            var nowLocal = DateTime.Now;
            var nowUtc = DateTime.UtcNow;

            foreach (var notification in _cachedNotifications.ToList())
            {
                try
                {
                    await ProcessNotificationAsync(notification, nowLocal, nowUtc);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process notification {NotificationId}", notification.Id);
                    WriteDebug($"Error processing notification {notification.Id}: {ex.Message}");
                }
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
        _logger.LogInformation("Loaded {Count} notifications for scheduling", _cachedNotifications.Count);
        WriteDebug($"Loaded {_cachedNotifications.Count} notifications for scheduling");
    }

    private async Task ProcessNotificationAsync(NotificationResponse notification, DateTime nowLocal, DateTime nowUtc)
    {
        if (!notification.IsActive)
        {
            WriteDebug($"Notification {notification.Id} ({notification.TagName ?? "(no tag name)"}) inactive; skipping");
            return;
        }

        WriteDebug($"Evaluating notification {notification.Id} ({notification.TagName ?? "(no tag name)"}) at {nowLocal:O}");

        var windows = NotificationScheduleCalculator.BuildWindows(notification, nowLocal);
        if (windows.Count == 0)
        {
            WriteDebug($"Notification {notification.Id}: no windows available");
            return;
        }

        var activeWindow = windows.FirstOrDefault(window => window.Contains(nowLocal));
        if (activeWindow is null)
        {
            WriteDebug($"Notification {notification.Id}: current time outside active windows");
            return;
        }

        var today = DateOnly.FromDateTime(nowLocal);
        var deliveriesToday = notification.LastDeliveryDate == today ? notification.DeliveriesOnLastDate : 0;
        var totalOccurrences = Math.Max(0, notification.MaxNudges) + 1;

        if (deliveriesToday >= totalOccurrences)
        {
            WriteDebug($"Notification {notification.Id}: daily limit reached ({deliveriesToday}/{totalOccurrences})");
            return;
        }

        var nextOccurrenceIndex = deliveriesToday;
        var scheduledTime = activeWindow.GetOccurrenceTime(nextOccurrenceIndex);
        if (nowLocal < scheduledTime)
        {
            WriteDebug($"Notification {notification.Id}: waiting for next occurrence at {scheduledTime:O}");
            return;
        }

        if (notification.NextEligibleSendAfterUtc.HasValue && notification.NextEligibleSendAfterUtc.Value > nowUtc)
        {
            WriteDebug($"Notification {notification.Id}: cooldown active until {notification.NextEligibleSendAfterUtc.Value:O}");
            return;
        }

        // Catch up on overdue occurrences while conditions allow.
        while (deliveriesToday < totalOccurrences)
        {
            nextOccurrenceIndex = deliveriesToday;
            scheduledTime = activeWindow.GetOccurrenceTime(nextOccurrenceIndex);

            if (nowLocal < scheduledTime)
            {
                WriteDebug($"Notification {notification.Id}: next occurrence at {scheduledTime:O}; stopping");
                break;
            }

            if (notification.NextEligibleSendAfterUtc.HasValue && notification.NextEligibleSendAfterUtc.Value > nowUtc)
            {
                WriteDebug($"Notification {notification.Id}: cooldown now extends to {notification.NextEligibleSendAfterUtc.Value:O}; stopping");
                break;
            }

            await DispatchAndPersistAsync(notification, today, deliveriesToday + 1);

            // After API call, notification reference is updated with latest counters
            deliveriesToday = notification.LastDeliveryDate == today ? notification.DeliveriesOnLastDate : 0;
            nowUtc = DateTime.UtcNow;
        }
    }

    private async Task DispatchAndPersistAsync(NotificationResponse notification, DateOnly today, int deliveriesOnDate)
    {
        var message = string.IsNullOrWhiteSpace(notification.NotificationText)
            ? NotificationScheduleCalculator.BuildDefaultMessage(notification)
            : notification.NotificationText!;

        var payload = new NotificationPayload
        {
            NotificationId = notification.Id,
            TagId = notification.TagId,
            TagName = notification.TagName,
            LocalDate = today
        };

        _notificationService.SendNotification("LogMyDay", message, notifyTime: null, payload: payload);
        _logger.LogDebug("Notification dispatched for tag {TagId}", notification.TagId);
        WriteDebug($"Notification dispatched for tag {notification.TagId}");

        var activityApi = _apiClientProvider.Activity;
        var occurredUtc = DateTime.UtcNow;
        var sanitizedInterval = NotificationScheduleCalculator.SanitizeInterval(notification.NudgeInterval);
        var nextEligibleUtc = occurredUtc.Add(sanitizedInterval);

        var request = new NotificationDeliveryRequest
        {
            OccurredAtUtc = occurredUtc,
            LocalDate = today,
            DeliveriesOnDate = deliveriesOnDate,
            NextEligibleSendAfterUtc = nextEligibleUtc
        };

        try
        {
            var updated = await activityApi.RecordNotificationDelivery(notification.Id, request);
            notification.LastDeliveryDate = updated.LastDeliveryDate;
            notification.DeliveriesOnLastDate = updated.DeliveriesOnLastDate;
            notification.LastDeliverySentAtUtc = updated.LastDeliverySentAtUtc;
            notification.NextEligibleSendAfterUtc = updated.NextEligibleSendAfterUtc;
            UpdateCachedNotification(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist delivery for notification {NotificationId}", notification.Id);
            WriteDebug($"Error persisting delivery for notification {notification.Id}: {ex.Message}");
            throw;
        }
    }

    private void UpdateCachedNotification(NotificationResponse updated)
    {
        for (var i = 0; i < _cachedNotifications.Count; i++)
        {
            if (_cachedNotifications[i].Id == updated.Id)
            {
                _cachedNotifications[i] = updated;
                break;
            }
        }
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
