using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LogMyDay.Domain.Enums;

namespace LogMyDay.Domain.Entities;

public class EventLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public required Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    public EventLogLevel Level { get; set; }

    [Required]
    [MaxLength(500)]
    public required string Message { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public EventLogDetail? Detail { get; set; }
}
