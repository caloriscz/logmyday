namespace LogMyDay.Shared.DTOs;

/// <summary>
/// Event Log type filter derived from message prefixes — there is no category column in the
/// database. Synced mobile diagnostics arrive as "[category] body" (see DiagnosticStore), all
/// other events use stable "Activity/Reminder/Todo list …" message prefixes.
/// </summary>
public enum EventLogCategoryFilter
{
    All = 0,
    ReminderDiag,
    NoDiagnostics,
    Activity,
    Reminder,
    TodoList
}
