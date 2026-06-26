using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class TodoListResponse
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int DisplayOrder { get; set; }
    public bool ShowOnHomepage { get; set; }
    public DateTime DateCreated { get; set; }

    public int? CompletionTagId { get; set; }
    public string? CompletionTagName { get; set; }
    public int? CompletionTagInputTypeId { get; set; }
    public AutoLogMode AutoLogMode { get; set; }

    public IList<TodoItemResponse> Items { get; set; } = new List<TodoItemResponse>();

    /// <summary>
    /// Compatibility shim — Reminder lists were split out into <see cref="ReminderListResponse"/>.
    /// Always returns Basic. Source-compat for razor markup not yet stripped.
    /// </summary>
    public TodoListType ListType => TodoListType.Basic;
}
