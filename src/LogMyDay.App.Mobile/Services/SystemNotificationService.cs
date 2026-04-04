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
    void MarkTagFulfilled(int tagId, DateOnly date);
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
    private readonly HashSet<(int tagId, DateOnly date)> _fulfilledTags = new();
    private const string LogPrefix = "SystemNotificationService";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(2);

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
        _fulfilledTags.Clear();
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

        var today = DateOnly.FromDateTime(nowLocal);

        // If the tag already has an activity logged today, suppress all notifications.
        if (await IsTagFulfilledAsync(notification.TagId, today))
        {
            WriteDebug($"Notification {notification.Id}: tag {notification.TagId} already fulfilled for {today}; cancelling alarms");
            _notificationService.CancelAlarmsForTag(notification.TagId);

            return;
        }

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

            // Pre-schedule alarms for the next upcoming window so they fire in the background.
            var nextWindow = windows
                .Where(w => w.Start > nowLocal)
                .OrderBy(w => w.Start)
                .FirstOrDefault();

            if (nextWindow is not null)
            {
                var windowDate = DateOnly.FromDateTime(nextWindow.Start);
                var totalOccurrencesForNext = Math.Max(0, notification.MaxNudges) + 1;
                ScheduleFutureOccurrences(notification, nextWindow, windowDate, 0, totalOccurrencesForNext, nowLocal);
                WriteDebug($"Notification {notification.Id}: pre-scheduled {totalOccurrencesForNext} alarms for window at {nextWindow.Start:O}");
            }

            return;
        }

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
            ScheduleFutureOccurrences(notification, activeWindow, today, deliveriesToday, totalOccurrences, nowLocal);

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

        // Schedule any remaining future occurrences via AlarmManager so they fire in background.
        ScheduleFutureOccurrences(notification, activeWindow, today, deliveriesToday, totalOccurrences, nowLocal);
    }

    private async Task<bool> IsTagFulfilledAsync(int tagId, DateOnly today)
    {
        var key = (tagId, today);
        if (_fulfilledTags.Contains(key))
        {
            return true;
        }

        try
        {
            var activityApi = _apiClientProvider.Activity;
            var isFulfilled = await activityApi.HasActivityForTag(tagId, today);
            if (isFulfilled)
            {
                _fulfilledTags.Add(key);
            }

            return isFulfilled;
        }
        catch (Exception ex)
        {
            WriteDebug($"Error checking fulfilled status for tag {tagId}: {ex.Message}; proceeding normally");

            return false;
        }
    }

    private void ScheduleFutureOccurrences(
        NotificationResponse notification,
        NotificationWindow activeWindow,
        DateOnly today,
        int deliveriesAlreadyDone,
        int totalOccurrences,
        DateTime nowLocal)
    {
        // Cancel existing alarms first to avoid duplicates on repeated checks.
        _notificationService.CancelAlarmsForNotification(notification.Id);

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

        for (var i = deliveriesAlreadyDone; i < totalOccurrences; i++)
        {
            var occurrenceTime = activeWindow.GetOccurrenceTime(i);
            if (occurrenceTime <= nowLocal)
            {
                continue;
            }

            _notificationService.SendNotification("LogMyDay", message, notifyTime: occurrenceTime, payload: payload);
            WriteDebug($"Notification {notification.Id}: scheduled occurrence {i + 1}/{totalOccurrences} at {occurrenceTime:O}");
        }
    }

    public void MarkTagFulfilled(int tagId, DateOnly date)
    {
        _fulfilledTags.Add((tagId, date));
        _notificationService.CancelAlarmsForTag(tagId);
        WriteDebug($"MarkTagFulfilled: tagId={tagId}, date={date}; alarms cancelled");
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
