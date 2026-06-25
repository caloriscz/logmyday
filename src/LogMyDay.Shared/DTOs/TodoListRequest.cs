using System.ComponentModel.DataAnnotations;
using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class TodoListRequest
{
    [Required]
    [MaxLength(200)]
    public required string Name { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public bool ShowOnHomepage { get; set; } = false;

    /// <summary>Auto-log tag shared by every item in the list; null = no auto-logging.</summary>
    public int? CompletionTagId { get; set; }

    public AutoLogMode AutoLogMode { get; set; } = AutoLogMode.Add;

    /// <summary>Compat shim — ignored by the server (always Basic). See <see cref="ReminderListRequest"/>.</summary>
    public TodoListType ListType { get; set; } = TodoListType.Basic;
}
