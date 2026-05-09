using System.ComponentModel.DataAnnotations;
using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class TodoListRequest
{
    [Required]
    [MaxLength(200)]
    public required string Name { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public TodoListType ListType { get; set; } = TodoListType.Basic;

    public bool ShowOnHomepage { get; set; } = false;
}
