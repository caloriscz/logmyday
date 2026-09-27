using LogMyDay.Domain.Enums;

namespace LogMyDay.Domain.Entities;

/// <summary>A Tag Activity Relations rule. Phase 1 knows one template: a weighted sum of numeric
/// source tags per local day, written as a generated activity on the rule's own computed target
/// tag. One rule owns its target tag (unique <see cref="TargetTagId"/>).</summary>
public class TagRule
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public TagRuleTemplate Template { get; set; } = TagRuleTemplate.WeightedSum;

    public int TargetTagId { get; set; }
    public Tag? TargetTag { get; set; }

    /// <summary>Skip source rows whose value is 0 (e.g. reminder skip markers).</summary>
    public bool IgnoreZero { get; set; } = true;

    public bool IsEnabled { get; set; } = true;

    /// <summary>Forward evaluation only touches days on or after this date.</summary>
    public DateOnly EffectiveFrom { get; set; }

    public DateTime DateCreated { get; set; }
    public DateTime DateUpdated { get; set; }

    public ICollection<TagRuleSource> Sources { get; set; } = new List<TagRuleSource>();
}
