using System.ComponentModel.DataAnnotations;

namespace LogMyDay.Shared.DTOs;

public class ReminderListRequest
{
    [Required]
    [MaxLength(200)]
    public required string Name { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public bool ShowOnHomepage { get; set; } = false;
}
