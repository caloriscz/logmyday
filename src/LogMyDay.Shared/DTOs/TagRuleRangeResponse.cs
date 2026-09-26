namespace LogMyDay.Shared.DTOs;

/// <summary>What recomputing a rule over a range changes (a preview) or changed (a run).</summary>
public class TagRuleRangeResponse
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    /// <summary>Days that get a value they did not have.</summary>
    public int Created { get; set; }

    /// <summary>Days whose value changes.</summary>
    public int Updated { get; set; }

    /// <summary>Days whose value is removed because no source value is left.</summary>
    public int Deleted { get; set; }

    /// <summary>Days whose value stays the same.</summary>
    public int Unchanged { get; set; }

    /// <summary>Source values that are not numbers and are left out.</summary>
    public int SkippedSourceValues { get; set; }

    /// <summary>The earliest day any source tag has a value; the natural start of a full recompute.</summary>
    public DateOnly? FirstSourceDate { get; set; }

    /// <summary>Calculated values before <see cref="From"/> that this range leaves untouched; they
    /// keep the rule's previous definition.</summary>
    public int ResultsBeforeFrom { get; set; }
}

public class TagRuleRecomputeRequest
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
}
