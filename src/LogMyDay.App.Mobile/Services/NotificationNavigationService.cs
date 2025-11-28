using System;
using Microsoft.Extensions.Logging;

namespace LogMyDay.App.Mobile.Services;

public class NotificationNavigationService
{
    private readonly object _syncRoot = new();
    private readonly ILogger<NotificationNavigationService> _logger;
    private NotificationPayload? _pendingPayload;

    public NotificationNavigationService(ILogger<NotificationNavigationService> logger)
    {
        _logger = logger;
    }

    public event EventHandler<NotificationPayload>? NavigationRequested;

    public NotificationPayload? PeekPendingIntent()
    {
        lock (_syncRoot)
        {
            return _pendingPayload;
        }
    }

    public void SetPendingIntent(NotificationPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        lock (_syncRoot)
        {
            _pendingPayload = payload;
        }

        _logger.LogInformation("Notification intent captured for notification {NotificationId} targeting tag {TagId}", payload.NotificationId, payload.TagId);
        NavigationRequested?.Invoke(this, payload);
    }

    public void ClearPendingIntent()
    {
        lock (_syncRoot)
        {
            _pendingPayload = null;
        }
    }
}
