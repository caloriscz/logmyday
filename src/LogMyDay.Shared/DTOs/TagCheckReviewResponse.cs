namespace LogMyDay.Shared.DTOs;

/// <summary>The days in a range that break an active check, newest first. Computed on request;
/// nothing is stored.</summary>
public class TagCheckReviewResponse
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }

    /// <summary>How many active checks were evaluated.</summary>
    public int CheckCount { get; set; }

    public List<TagCheckReviewItem> Items { get; set; } = new();
}

public class TagCheckReviewItem
{
    public DateOnly Date { get; set; }
    public int CheckId { get; set; }
    public string CheckName { get; set; } = string.Empty;

    /// <summary>Flag or Warn.</summary>
    public string Severity { get; set; } = "Flag";

    /// <summary>Today, the second tag is expected but not logged yet; the day is not over.</summary>
    public bool IsPending { get; set; }

    public string IfTagName { get; set; } = string.Empty;

    /// <summary>The first tag's day value: the total of a number tag, the average of a rating,
    /// score or percentage, the latest entry otherwise (yes/no as true/false, an option by value).</summary>
    public string? IfValue { get; set; }

    public string ThenTagName { get; set; } = string.Empty;

    /// <summary>The second tag's day value as <see cref="IfValue"/>; null when it has no entry that day.</summary>
    public string? ThenValue { get; set; }
}
