using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogMyDay.Domain.Entities;

public class DashboardPanel
{
    public int Id { get; set; }

    public int DashboardId { get; set; }

    [ForeignKey(nameof(DashboardId))]
    public Dashboard Dashboard { get; set; } = null!;

    public int WidgetTypeId { get; set; }

    public int? TagId { get; set; }

    [ForeignKey(nameof(TagId))]
    public Tag? Tag { get; set; }

    public string? Parameters { get; set; }

    public int SizeId { get; set; }

    public int DisplayOrder { get; set; }

    [MaxLength(100)]
    public string? Title { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
}
