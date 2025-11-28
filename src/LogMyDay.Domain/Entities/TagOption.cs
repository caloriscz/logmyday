using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogMyDay.Domain.Entities;

public class TagOption
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int OptionListId { get; set; }

    [ForeignKey(nameof(OptionListId))]
    public TagOptionList? OptionList { get; set; }

    [Required]
    public string Value { get; set; } = string.Empty;

    public string? DisplayName { get; set; }
}
