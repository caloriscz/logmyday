using System.ComponentModel.DataAnnotations;

namespace LogMyDay.Shared.DTOs;

public class TodoListRequest
{
    [Required]
    [MaxLength(200)]
    public required string Name { get; set; }

    public int? CompletionTagId { get; set; }

    public int DisplayOrder { get; set; } = 0;
}
