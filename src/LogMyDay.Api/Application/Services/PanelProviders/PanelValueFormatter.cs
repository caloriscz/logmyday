namespace LogMyDay.Api.Application.Services.PanelProviders;

internal static class PanelValueFormatter
{
    internal static bool TryParseDecimal(string? description, out decimal value)
    {
        return decimal.TryParse(description,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);
    }

    internal static string Format(decimal value, string? unitSymbol)
    {
        var formatted = value == Math.Floor(value)
            ? value.ToString("0")
            : value.ToString("0.##");

        return string.IsNullOrWhiteSpace(unitSymbol) ? formatted : $"{formatted} {unitSymbol}";
    }
}
