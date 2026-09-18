using System.Globalization;

namespace LogMyDay.App.Services.Insights;

/// <summary>
/// Collapses a day's activity values into the single value a timeline cell shows. Pure: the display
/// culture is always a parameter, and the numeric reading it produces stays invariant so the colour
/// scheme resolver and the user's formatting never disagree.
/// </summary>
public static class ComparisonAggregator
{
    private const string AverageFormat = "0.##";
    private const string ScalarFormat = "0.####";
    private const string BooleanShareFormat = "P0";

    public const string TrueGlyph = "\u2713";
    public const string FalseGlyph = "\u2717";

    /// <summary>
    /// Aggregates one day. A day with no entries is <see cref="ComparisonCellState.Missing"/> in every
    /// mode — including <see cref="ComparisonAggregation.Count"/>, so that "nothing logged" stays visually
    /// distinct from "logged a zero", as it is in Calendar.
    ///
    /// For a Boolean tag (<paramref name="isBoolean"/>) "true"/"false" read as 1/0, so every mode works
    /// and means something: Max is "any true that day", Average is the share of true, Sum counts trues.
    /// The flag is per tag, not per value, so a String tag holding the word "true" still renders as text.
    /// </summary>
    public static ComparisonCell Aggregate(
        DateTime actualDate,
        IReadOnlyList<string>? rawValues,
        ComparisonAggregation mode,
        bool isBoolean = false)
    {
        if (rawValues is null || rawValues.Count == 0)
        {
            return ComparisonCell.Missing(actualDate);
        }

        if (mode == ComparisonAggregation.Count)
        {
            return ComparisonCell.Numeric(actualDate, rawValues, rawValues.Count);
        }

        if (mode == ComparisonAggregation.First)
        {
            // The only mode that can surface a non-numeric value: a string or option-list entry is shown
            // verbatim, it just gets no scheme colour (ColorSchemeIndex only resolves numbers).
            return TryParseValue(rawValues[0], isBoolean, out var first)
                ? ComparisonCell.Numeric(actualDate, rawValues, first)
                : ComparisonCell.Text(actualDate, rawValues);
        }

        var numbers = ParseNumbers(rawValues, isBoolean);

        if (numbers.Count == 0)
        {
            return ComparisonCell.NotApplicable(actualDate, rawValues);
        }

        var aggregated = mode switch
        {
            ComparisonAggregation.Sum => numbers.Sum(),
            ComparisonAggregation.Average => numbers.Average(),
            ComparisonAggregation.Max => numbers.Max(),
            _ => numbers[0]
        };

        return ComparisonCell.Numeric(actualDate, rawValues, aggregated);
    }

    /// <summary>
    /// The text shown in the cell, in the user's culture. Deliberately separate from
    /// <see cref="ComparisonCell.ColorKey"/>, which stays invariant for the colour resolver.
    /// </summary>
    public static string? Format(ComparisonCell cell, ComparisonAggregation mode, IFormatProvider provider, bool isBoolean = false)
    {
        if (cell.State != ComparisonCellState.Value)
        {
            return null;
        }

        // A boolean First/Max is a single truth value: a glyph reads better than "1"/"0" or the raw
        // "true"/"false" text, and works inside a scheme-coloured badge where the icon's own colours
        // would clash. Average is a share, so it formats as a percentage.
        if (isBoolean && mode is ComparisonAggregation.First or ComparisonAggregation.Max && cell.NumericValue is decimal flag)
        {
            return flag >= 1 ? TrueGlyph : FalseGlyph;
        }

        if (mode == ComparisonAggregation.First)
        {
            return cell.RawValues.Count > 0 ? cell.RawValues[0] : null;
        }

        if (cell.NumericValue is not decimal value)
        {
            return null;
        }

        return mode switch
        {
            ComparisonAggregation.Count => ((int)value).ToString(provider),
            ComparisonAggregation.Average when isBoolean => value.ToString(BooleanShareFormat, provider),
            ComparisonAggregation.Average => value.ToString(AverageFormat, provider),
            _ => value.ToString(ScalarFormat, provider)
        };
    }

    /// <summary>
    /// Whether the cell should render the boolean icon rather than text: a Boolean tag under a mode that
    /// yields a single truth value, with no scheme colour to paint the badge. When a scheme colour did
    /// resolve, the caller shows the glyph from <see cref="Format"/> on the coloured badge instead.
    /// </summary>
    public static bool ShowsBooleanIcon(ComparisonCell cell, ComparisonAggregation mode, bool isBoolean)
    {
        return isBoolean
            && mode is ComparisonAggregation.First or ComparisonAggregation.Max
            && cell.State == ComparisonCellState.Value
            && cell.NumericValue is not null;
    }

    /// <summary>
    /// Whether an aggregated value still sits on the tag's own scale, and so whether its colour scheme
    /// means anything. Sum and Count leave the scale — a Count of 3 against a 0–5 star scheme would paint
    /// a reassuring green that says nothing about the data — so those cells render uncoloured.
    /// </summary>
    public static bool PreservesScale(ComparisonAggregation mode)
    {
        return mode is not (ComparisonAggregation.Sum or ComparisonAggregation.Count);
    }

    private static List<decimal> ParseNumbers(IReadOnlyList<string> rawValues, bool isBoolean)
    {
        var numbers = new List<decimal>(rawValues.Count);

        foreach (var raw in rawValues)
        {
            if (TryParseValue(raw, isBoolean, out var value))
            {
                numbers.Add(value);
            }
        }

        return numbers;
    }

    /// <summary>
    /// Matches <c>ChartDataService</c>'s numeric parsing exactly, so numeric behaviour agrees with Charts.
    /// Boolean tags additionally read "true"/"false" as 1/0; a literal "1"/"0" still parses numerically.
    /// </summary>
    private static bool TryParseValue(string? raw, bool isBoolean, out decimal value)
    {
        if (isBoolean && bool.TryParse(raw, out var flag))
        {
            value = flag ? 1 : 0;

            return true;
        }

        return decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
