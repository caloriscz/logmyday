using LogMyDay.Shared.DTOs;
using System.Globalization;

namespace LogMyDay.Shared.Notifications;

public static class NotificationScheduleCalculator
{
    public const int MinimumIntervalMinutes = 5;
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMinutes(15);

    public static TimeSpan SanitizeInterval(TimeSpan? interval)
    {
        var sanitized = interval ?? DefaultInterval;

        if (sanitized <= TimeSpan.Zero)
        {
            sanitized = TimeSpan.FromMinutes(MinimumIntervalMinutes);
        }

        if (sanitized.TotalMinutes < MinimumIntervalMinutes)
        {
            sanitized = TimeSpan.FromMinutes(MinimumIntervalMinutes);
        }

        return sanitized;
    }

    public static IReadOnlyList<NotificationWindow> BuildWindows(NotificationResponse notification, DateTime referenceDate)
    {
        if (notification is null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        if (!notification.IsActive)
        {
            return Array.Empty<NotificationWindow>();
        }

        var notBefore = notification.NotBeforeTime ?? TimeSpan.Zero;
        if (notBefore < TimeSpan.Zero)
        {
            notBefore = TimeSpan.Zero;
        }

        var notAfter = notification.NotAfterTime;
        if (notAfter.HasValue && notAfter.Value < notBefore)
        {
            // Guard against persistence anomalies; treat as no upper bound when invalid
            notAfter = null;
        }

        var interval = SanitizeInterval(notification.NudgeInterval);
        var totalOccurrences = Math.Max(0, notification.MaxNudges) + 1;

        var maxSpan = notAfter.HasValue
            ? TimeSpan.FromTicks(Math.Max(notBefore.Ticks, notAfter.Value.Ticks))
            : notBefore;

        var daysToConsider = Math.Max(1, (int)Math.Floor(maxSpan.TotalDays) + 1);
        var windows = new List<NotificationWindow>(capacity: daysToConsider);

        for (var offset = 0; offset < daysToConsider; offset++)
        {
            var baseDate = DateOnly.FromDateTime(referenceDate.Date.AddDays(-offset));
            var baseDateTime = baseDate.ToDateTime(TimeOnly.MinValue);
            var start = baseDateTime + notBefore;
            DateTime? end = notAfter.HasValue ? baseDateTime + notAfter.Value : null;

            windows.Add(new NotificationWindow(baseDate, start, end, totalOccurrences, interval));
        }

        return windows;
    }

    public static string BuildDefaultMessage(NotificationResponse notification)
    {
        var tagLabel = notification.TagName;
        if (string.IsNullOrWhiteSpace(tagLabel))
        {
            tagLabel = "your activity";
        }

        return string.Format(CultureInfo.CurrentCulture, "Don't forget to log {0}.", tagLabel);
    }
}

public sealed record NotificationWindow(
    DateOnly BaseDate,
    DateTime Start,
    DateTime? End,
    int TotalOccurrences,
    TimeSpan Interval)
{
    public DateTime GetOccurrenceTime(int occurrenceIndex)
    {
        if (occurrenceIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(occurrenceIndex));
        }

        if (occurrenceIndex >= TotalOccurrences)
        {
            throw new ArgumentOutOfRangeException(nameof(occurrenceIndex));
        }

        if (occurrenceIndex == 0)
        {
            return Start;
        }

        var offset = TimeSpan.FromTicks(Interval.Ticks * occurrenceIndex);
        return Start + offset;
    }

    public bool Contains(DateTime timestamp)
    {
        if (timestamp < Start)
        {
            return false;
        }

        if (End.HasValue && timestamp > End.Value)
        {
            return false;
        }

        return true;
    }
}
