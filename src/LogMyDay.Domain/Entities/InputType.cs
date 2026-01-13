using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogMyDay.Domain.Entities;

public class InputType
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }
    public string Name { get; set; }
    public bool IsRangeEditable { get; set; } = true;
    public bool IsMinimumEditable { get; set; } = true;
    public bool IsMaximumEditable { get; set; } = true;
    public bool IsStepEditable { get; set; } = true;
    public bool IsRepeatableEditable { get; set; } = true;
    public string? Description { get; set; }
}
