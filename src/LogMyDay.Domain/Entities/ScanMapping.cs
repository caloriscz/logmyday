using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LogMyDay.Domain.Enums;

namespace LogMyDay.Domain.Entities;

public class ScanMapping
{
    public int Id { get; set; }

    public required Guid UserId { get; set; }

    [MaxLength(512)]
    public required string CodeValue { get; set; }

    public CodeType CodeType { get; set; }

    public required int TagId { get; set; }

    [ForeignKey(nameof(TagId))]
    public Tag Tag { get; set; } = null!;

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    public string? DefaultDescription { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
}
