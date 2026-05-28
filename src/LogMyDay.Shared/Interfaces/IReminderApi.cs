using LogMyDay.Shared.DTOs;
using Refit;

namespace LogMyDay.Shared.Interfaces;

public interface IReminderApi
{
    [Get("/api/reminder-lists")]
    Task<IList<ReminderListResponse>> GetReminderLists([AliasAs("date")] string? date = null);

    [Post("/api/reminder-lists")]
    Task<ReminderListResponse> CreateReminderList([Body] ReminderListRequest request);

    [Put("/api/reminder-lists/{id}")]
    Task UpdateReminderList(int id, [Body] ReminderListRequest request);

    [Delete("/api/reminder-lists/{id}")]
    Task DeleteReminderList(int id);

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

    [Patch("/api/reminder-lists/{listId}/items/reorder")]
    Task ReorderReminders(int listId, [Body] IList<ReminderReorderRequest> items);
}
