using LogMyDay.Domain.Enums;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogMyDay.Domain.Entities;
public class Tag
{
    [Key]
    public int Id { get; set; }

    [Required]
    public required string TagName { get; set; }

    public int? InputTypeId { get; set; }

    [ForeignKey(nameof(InputTypeId))]
    public InputType? InputType { get; set; }

    public bool IsRequired { get; set; } = false;

    public TimeGranularity TimeGranularity { get; set; } = TimeGranularity.Exact;
    
    public bool IsRepeatable { get; set; } = true;
    public bool IsRange { get; set; } = false;

    public int? PatternId { get; set; }

    [ForeignKey(nameof(PatternId))]
    public Pattern? Pattern { get; set; }
    public Guid? UserId { get; set; }

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}