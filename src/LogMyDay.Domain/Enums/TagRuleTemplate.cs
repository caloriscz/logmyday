namespace LogMyDay.Domain.Enums;

public enum TagRuleTemplate
{
    /// <summary>Σ (source value × factor) per local day.</summary>
    WeightedSum = 0,

    /// <summary>Average, minimum or maximum of the day's source values, or the count of the
    /// day's source entries (see <see cref="TagRuleAggregateKind"/>).</summary>
    Aggregate = 1,

    /// <summary>The value of the first case whose condition holds on the source's day value;
    /// presence is a single "is logged" case.</summary>
    Conditional = 2
}

public enum TagRuleAggregateKind
{
    Average = 0,
    Minimum = 1,
    Maximum = 2,

    /// <summary>The number of the day's source entries, of any tag type.</summary>
    Count = 3
}

/// <summary>The test a conditional case applies to the source's day value.</summary>
public enum TagRuleOperator
{
    /// <summary>The source has an entry that day.</summary>
    IsLogged = 0,

    /// <summary>Always matches; only allowed as the last case.</summary>
    Otherwise = 1,

    Equal = 2,
    NotEqual = 3,
    Greater = 4,
    GreaterOrEqual = 5,
    Less = 6,
    LessOrEqual = 7,
    IsYes = 8,
    IsNo = 9
}

/// <summary>How a conditional rule reads its source tag's day value and which tests apply.</summary>
public enum TagRuleValueKind
{
    /// <summary>Integer or decimal: the day total, compared as a number.</summary>
    Number,

    /// <summary>Rating, score or percentage: the day average, compared as a number.</summary>
    Scale,

    /// <summary>Yes/no: the latest entry, tested with "is yes" / "is no".</summary>
    YesNo,

    /// <summary>Text, date, time or any tag with an option list: the latest entry, compared as text.</summary>
    Text
}
