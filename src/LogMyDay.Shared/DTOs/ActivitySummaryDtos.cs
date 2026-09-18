namespace LogMyDay.Shared.DTOs;

public enum SummaryBucket
{
    Day = 0,
    Week = 1,
    Month = 2
}

public class ActivitySummaryRequest
{
    public int TagId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public SummaryBucket Bucket { get; set; } = SummaryBucket.Day;
}

/// <summary>
/// Server-side statistics for one tag over a date range. Values are bucketed by the stored
/// <c>DateStarted</c> date with no time-zone conversion (<see cref="Convention"/>), which is what the
/// web Insights, the activity list and the period rules use.
/// </summary>
public class ActivitySummaryResponse
{
    public const string StoredLocalDate = "stored-local-date";
    public const string DayUnit = "day";

    public int TagId { get; set; }
    public required string Tag { get; set; }
    public int? InputTypeId { get; set; }
    public string? InputType { get; set; }
    /// <summary>numeric | boolean | categorical | text | date | time</summary>
    public required string ValueKind { get; set; }
    public string? UnitSymbol { get; set; }

    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public SummaryBucket Bucket { get; set; }
    public string Convention { get; set; } = StoredLocalDate;
    public DayOfWeek WeekStartsOn { get; set; }

    public int Count { get; set; }
    public int DaysWithData { get; set; }
    public int DaysInRange { get; set; }
    /// <summary>Rows whose value could not be read for the value kind; counted, excluded from the maths.</summary>
    public int InvalidValueCount { get; set; }

    /// <summary>Numeric kinds. For boolean: sum = number of true, average = share of true.</summary>
    public double? Sum { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Average { get; set; }

    public SummaryPoint? First { get; set; }
    public SummaryPoint? Last { get; set; }

    /// <summary>Consecutive days with data ending at To (or To − 1 when To has none yet).</summary>
    public int CurrentStreak { get; set; }
    public DateOnly? CurrentStreakEnd { get; set; }
    public int LongestStreak { get; set; }
    public DateOnly? LongestStreakStart { get; set; }
    public DateOnly? LongestStreakEnd { get; set; }
    public string StreakUnit { get; set; } = DayUnit;

    /// <summary>One row per bucket across the whole range, empty buckets included.</summary>
    public List<SummaryBucketRow> Buckets { get; set; } = [];

    /// <summary>Most frequent values (top 20) for categorical and text kinds.</summary>
    public List<ValueFrequency>? Frequencies { get; set; }
}

public class SummaryPoint
{
    public required string Value { get; set; }
    public DateTime DateStarted { get; set; }
}

public class SummaryBucketRow
{
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }
    public int Count { get; set; }
    public int DaysWithData { get; set; }
    public double? Sum { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Average { get; set; }
}

public class ValueFrequency
{
    public required string Value { get; set; }
    public int Count { get; set; }
    public double Share { get; set; }
}
