using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogMyDay.Domain.Entities;

public class ReminderList
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public Guid UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Name { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public bool ShowOnHomepage { get; set; } = false;

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    public ICollection<Reminder> Items { get; set; } = new List<Reminder>();
}
