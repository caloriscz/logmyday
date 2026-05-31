namespace LogMyDay.Domain.Enums;

/// <summary>
/// Legacy enum retained for source compatibility while the Reminder UI flip lands incrementally.
/// Production `TodoList` rows are always Basic post-2026-05-26 migration; Reminder data lives
/// in `ReminderList` / `Reminder` entities. The Reminder member here is a no-op marker for
/// remaining razor branches that haven't been stripped yet.
/// </summary>
[System.Obsolete("Use ReminderList for reminder-type lists; TodoList is Basic-only now.")]
public enum TodoListType
{
    Basic = 0,
    Reminder = 1
}
