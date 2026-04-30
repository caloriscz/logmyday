using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogMyDay.Domain.Entities;

public class TodoList
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public Guid UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public required string Name { get; set; }

    public int? CompletionTagId { get; set; }

    [ForeignKey(nameof(CompletionTagId))]
    public Tag? CompletionTag { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    public ICollection<TodoItem> Items { get; set; } = new List<TodoItem>();
}
