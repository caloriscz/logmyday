using System.Globalization;

namespace LogMyDay.Api.Application.Services;

/// <summary>Reads and writes numeric activity values. Activity values are strings in
/// <c>Description</c>; numbers are written with the invariant culture. Older rows may carry a
/// decimal comma (they were formatted in the server culture), so a comma is accepted as the
/// decimal separator when reading.</summary>
public static class ActivityValueCodec
{
    private const int IntegerInputType = 1;

    public static bool TryReadNumber(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || (trimmed.Count(c => c == ',') == 1 && !trimmed.Contains('.')
                && double.TryParse(trimmed.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value));
    }

    /// <summary>Integer tags are rounded to a whole number; every other numeric tag (Decimal,
    /// precision 2) is rounded to two decimals and written without trailing zeros.</summary>
    public static string WriteNumber(int? inputTypeId, double value)
    {
        return inputTypeId == IntegerInputType
            ? Math.Round(value, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)
            : Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
