namespace LogMyDay.Api.Application.Interfaces;

public interface IReminderDayBackfill
{
    /// <summary>
    /// One-time, idempotent backfill of <c>LogMyDay_ReminderDays</c> from the legacy single
    /// <c>DoneAt</c>/<c>SkippedAt</c> scalars on each reminder. No-op once it has run.
    /// </summary>
    Task RunAsync(CancellationToken cancellationToken = default);
}
