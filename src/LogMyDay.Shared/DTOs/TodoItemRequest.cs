using System.ComponentModel.DataAnnotations;

namespace LogMyDay.Shared.DTOs;

public class TodoItemRequest
{
    public int ListId { get; set; }

    [Required]
    [MaxLength(500)]
    public required string Title { get; set; }

    public string? Notes { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public TimeOnly? NotifyAt { get; set; }

    public int DisplayOrder { get; set; } = 0;
}
