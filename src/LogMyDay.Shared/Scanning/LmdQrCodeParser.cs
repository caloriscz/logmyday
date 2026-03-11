namespace LogMyDay.Shared.Scanning;

/// <summary>
/// Parses app-generated QR codes using the lmd:// URI scheme.
/// Format: lmd://tag/{id}?v={value}&amp;n={displayName}
/// </summary>
public static class LmdQrCodeParser
{
    private const string Scheme = "lmd://";
    private const string TagSegment = "tag/";

    public static LmdQrParseResult Parse(string? scannedValue)
    {
        if (string.IsNullOrWhiteSpace(scannedValue))
        {
            return LmdQrParseResult.NotAppFormatted;
        }

        if (!scannedValue.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return LmdQrParseResult.NotAppFormatted;
        }

        var remainder = scannedValue[Scheme.Length..];

        if (!remainder.StartsWith(TagSegment, StringComparison.OrdinalIgnoreCase))
        {
            return LmdQrParseResult.NotAppFormatted;
        }

        remainder = remainder[TagSegment.Length..];

        var queryIndex = remainder.IndexOf('?');
        var idPart = queryIndex >= 0 ? remainder[..queryIndex] : remainder;

        if (!int.TryParse(idPart, out var tagId) || tagId <= 0)
        {
            return LmdQrParseResult.NotAppFormatted;
        }

        string? value = null;
        string? displayName = null;

        if (queryIndex >= 0 && queryIndex < remainder.Length - 1)
        {
            var queryString = remainder[(queryIndex + 1)..];
            var parameters = ParseQueryString(queryString);

            if (parameters.TryGetValue("v", out var v))
            {
                value = v;
            }

            if (parameters.TryGetValue("n", out var n))
            {
                displayName = n;
            }
        }

        return new LmdQrParseResult
        {
            IsAppFormatted = true,
            TagId = tagId,
            Value = value,
            DisplayName = displayName
        };
    }

    private static Dictionary<string, string> ParseQueryString(string queryString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in queryString.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equalsIndex = pair.IndexOf('=');
            if (equalsIndex < 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..equalsIndex]);
            var val = Uri.UnescapeDataString(pair[(equalsIndex + 1)..].Replace('+', ' '));

            result[key] = val;
        }

        return result;
    }
}
