using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogMyDay.Domain.Entities;

public class Unit
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Symbol { get; set; } = string.Empty;

    public int QuantityId { get; set; }

    [ForeignKey(nameof(QuantityId))]
    public Quantity? Quantity { get; set; }

    public double AToBase { get; set; }
    public double BToBase { get; set; }

    public int Decimals { get; set; } = 0;

    public double ToBase(double value) => (AToBase * value) + BToBase;
    public double FromBase(double baseValue) => (baseValue - BToBase) / AToBase;
}
