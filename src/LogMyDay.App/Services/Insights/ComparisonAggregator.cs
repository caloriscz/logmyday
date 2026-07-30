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

    /// <summary>
    /// Aggregates one day. A day with no entries is <see cref="ComparisonCellState.Missing"/> in every
    /// mode — including <see cref="ComparisonAggregation.Count"/>, so that "nothing logged" stays visually
    /// distinct from "logged a zero", as it is in Calendar.
    /// </summary>
    public static ComparisonCell Aggregate(
        DateTime actualDate,
        IReadOnlyList<string>? rawValues,
        ComparisonAggregation mode)
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
            return TryParseValue(rawValues[0], out var first)
                ? ComparisonCell.Numeric(actualDate, rawValues, first)
                : ComparisonCell.Text(actualDate, rawValues);
        }

        var numbers = ParseNumbers(rawValues);

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
    public static string? Format(ComparisonCell cell, ComparisonAggregation mode, IFormatProvider provider)
    {
        if (cell.State != ComparisonCellState.Value)
        {
            return null;
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
            ComparisonAggregation.Average => value.ToString(AverageFormat, provider),
            _ => value.ToString(ScalarFormat, provider)
        };
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

    private static List<decimal> ParseNumbers(IReadOnlyList<string> rawValues)
    {
        var numbers = new List<decimal>(rawValues.Count);

        foreach (var raw in rawValues)
        {
            if (TryParseValue(raw, out var value))
            {
                numbers.Add(value);
            }
        }

        return numbers;
    }

    /// <summary>Matches <c>ChartDataService</c>'s parsing exactly, so numeric behaviour agrees with Charts.</summary>
    private static bool TryParseValue(string? raw, out decimal value)
    {
        return decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
