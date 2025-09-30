using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Notifications;

namespace LogMyDay.Api.Tests;

public class NotificationScheduleCalculatorTests
{
    [Fact]
    public void BuildWindows_ShouldIncludePreviousDayWhenNotBeforeExtendsPastMidnight()
    {
        // Arrange
        var notification = new NotificationResponse
        {
            Id = 1,
            TagId = 10,
            IsActive = true,
            NotBeforeTime = TimeSpan.FromHours(32), // next day at 08:00
            MaxNudges = 0
        };

        var reference = new DateTime(2025, 9, 30, 8, 30, 0);

        // Act
        var windows = NotificationScheduleCalculator.BuildWindows(notification, reference);

        // Assert
    Assert.Equal(2, windows.Count);

        var todayWindow = windows[0];
    Assert.Equal(DateOnly.FromDateTime(reference.Date), todayWindow.BaseDate);
    Assert.Equal(reference.Date.AddHours(32), todayWindow.Start);

        var previousWindow = windows[1];
    Assert.Equal(DateOnly.FromDateTime(reference.Date.AddDays(-1)), previousWindow.BaseDate);
    Assert.Equal(reference.Date.AddDays(-1).AddHours(32), previousWindow.Start);
    }

    [Fact]
    public void BuildWindows_ShouldSkipInactiveNotification()
    {
        var notification = new NotificationResponse
        {
            IsActive = false
        };

        var windows = NotificationScheduleCalculator.BuildWindows(notification, DateTime.UtcNow);

    Assert.Empty(windows);
    }

    [Theory]
    [InlineData(null, 15)]
    [InlineData(1, 5)]
    [InlineData(5, 5)]
    [InlineData(20, 20)]
    public void SanitizeInterval_ShouldEnforceMinimumMinutes(int? requestedMinutes, int expectedMinutes)
    {
        TimeSpan? requested = requestedMinutes.HasValue ? TimeSpan.FromMinutes(requestedMinutes.Value) : null;

        var sanitized = NotificationScheduleCalculator.SanitizeInterval(requested);

    Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), sanitized);
    }

    [Fact]
    public void BuildDefaultMessage_ShouldFallbackToTagName()
    {
        var notification = new NotificationResponse
        {
            TagName = "Daily rating",
            NotificationText = null
        };

        var message = NotificationScheduleCalculator.BuildDefaultMessage(notification);

    Assert.Contains("Daily rating", message);
    }
}
