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
}