namespace LogMyDay.Api.Application.Interfaces;

public interface IReminderRecurrenceMigration
{
    /// <summary>
    /// One-time, idempotent conversion of any reminder stored with
    /// <c>RecurrenceType.None</c> to <c>Daily</c>. None is no longer a valid reminder option.
    /// No-op once it has run.
    /// </summary>
    Task RunAsync(CancellationToken cancellationToken = default);
}
