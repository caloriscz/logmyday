using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogMyDay.Domain.Entities;

public class EventLogDetail
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public required int EventLogId { get; set; }

    [ForeignKey(nameof(EventLogId))]
    public EventLog EventLog { get; set; } = null!;

    [Required]
    public required string Description { get; set; }
}
