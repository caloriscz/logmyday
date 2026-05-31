using LogMyDay.Shared.DTOs;
using Refit;

namespace LogMyDay.Shared.Interfaces;

public interface IReminderApi
{
    [Get("/api/reminders")]
    Task<IList<ReminderResponse>> GetReminders([AliasAs("date")] string? date = null);

    [Post("/api/reminders")]
    Task<ReminderResponse> CreateReminder([Body] ReminderRequest request);

    [Put("/api/reminders/{id}")]
    Task UpdateReminder(int id, [Body] ReminderRequest request);

    [Delete("/api/reminders/{id}")]
    Task DeleteReminder(int id);

    [Post("/api/reminders/{id}/complete")]
    Task<ReminderResponse> CompleteReminder(int id, [Body] ReminderCompleteRequest request);

    [Post("/api/reminders/{id}/reopen")]
    Task<ReminderResponse> ReopenReminder(int id);

    [Post("/api/reminders/{id}/skip")]
    Task<ReminderResponse> SkipReminder(int id, [AliasAs("date")] string? date = null);

    [Post("/api/reminders/{id}/unskip")]
    Task<ReminderResponse> UnskipReminder(int id);

    [Patch("/api/reminders/reorder")]
    Task ReorderReminders([Body] IList<ReminderReorderRequest> items);
}
