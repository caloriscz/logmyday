using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogMyDay.Domain.Entities;

public class TagOptionList
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public Guid? UserId { get; set; }

    public ICollection<TagOption> Options { get; set; } = new List<TagOption>();
}
