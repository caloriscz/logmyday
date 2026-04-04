using System.ComponentModel.DataAnnotations;

namespace LogMyDay.Domain.Entities;

public class Dashboard
{
    public int Id { get; set; }

    public Guid UserId { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = "My Dashboard";

    public bool IsDefault { get; set; } = true;

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    public ICollection<DashboardPanel> Panels { get; set; } = new List<DashboardPanel>();
}
