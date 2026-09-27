using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace LogMyDay.Domain.Entities;

public class Activity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public DateTime DateCreated { get; set; }

    [Required]
    public DateTime DateStarted { get; set; }
    public DateTime? DateFinished { get; set; }
    public string? Description { get; set; }
    public Guid? UserId { get; set; }

    public int TagId { get; set; }
    [ForeignKey(nameof(TagId))]
    public Tag Tag { get; set; }

    /// <summary>The Tag Activity Relations rule that generated this row; null for entries
    /// logged by hand. A generated row is read-only.</summary>
    public int? RuleId { get; set; }

    /// <summary>The window a generated row covers (the local date, yyyy-MM-dd). With
    /// <see cref="RuleId"/> it identifies the result: one row per rule and window.</summary>
    [MaxLength(10)]
    public string? WindowKey { get; set; }
}