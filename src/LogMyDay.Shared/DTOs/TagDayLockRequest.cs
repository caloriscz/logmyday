using System.ComponentModel.DataAnnotations;

namespace LogMyDay.Shared.DTOs;

public class TagDayLockRequest
{
    [Required]
    public int TagId { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public bool IsLocked { get; set; }

    [MaxLength(200)]
    public string? Reason { get; set; }
}
