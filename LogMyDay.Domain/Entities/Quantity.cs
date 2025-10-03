using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogMyDay.Domain.Entities;

public class Quantity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Key { get; set; } = string.Empty;

    public int? BaseUnitId { get; set; }

    [ForeignKey(nameof(BaseUnitId))]
    public Unit? BaseUnit { get; set; }
}
