using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LogMyDay.Domain.Enums;

namespace LogMyDay.Domain.Entities;

/// <summary>
/// Basic todo list. Reminder-type lists were moved to <see cref="ReminderList"/> in
/// migration <c>20260524_SplitReminderEntity</c>; the <c>ListType</c> discriminator was
/// dropped together with the Reminder-only fields on <see cref="TodoItem"/>.
/// </summary>
public class TodoList
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public Guid UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Name { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public bool ShowOnHomepage { get; set; } = false;

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    /// <summary>Auto-log tag shared by every item in this list; null = the list does not auto-log.</summary>
    public int? CompletionTagId { get; set; }

    [ForeignKey(nameof(CompletionTagId))]
    public Tag? CompletionTag { get; set; }

    /// <summary>How a completed item's activity is written to <see cref="CompletionTag"/>.</summary>
    public AutoLogMode AutoLogMode { get; set; } = AutoLogMode.Add;

    public ICollection<TodoItem> Items { get; set; } = new List<TodoItem>();
}
