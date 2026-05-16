using System;

namespace LogMyDay.App.Mobile.Services;

public sealed class NotificationPayload
{
    public int NotificationId { get; init; }

    public int? TagId { get; init; }

    public string? TagName { get; init; }

    public DateOnly? LocalDate { get; init; }

    public int? TodoItemId { get; init; }

    public bool AutoComplete { get; init; }
}
