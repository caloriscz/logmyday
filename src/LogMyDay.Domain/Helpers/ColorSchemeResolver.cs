namespace LogMyDay.Domain.Helpers;

/// <summary>
/// Single source of truth for turning a numeric value into a color. When a tag has an assigned
/// color scheme, its entries win (first entry by <see cref="ColorSchemeEntry.SortOrder"/> whose
/// range contains the value; a null bound is open-ended). Otherwise the direction-aware default for
/// the input type applies. Returns null when nothing applies (e.g. a value outside every assigned
/// entry, or an Integer/Decimal tag with no scheme).
/// </summary>
public static class ColorSchemeResolver
{
    public static string? Resolve(int inputTypeId, IReadOnlyList<IColorEntry>? assignedEntries, double value)
    {
        if (assignedEntries is { Count: > 0 })
        {
            foreach (var entry in assignedEntries.OrderBy(e => e.SortOrder))
            {
                if (Contains(entry, value))
                {
                    return entry.Color;
                }
            }

            return null;
        }

        return DefaultColorSchemes.Resolve(inputTypeId, value);
    }

    private static bool Contains(IColorEntry entry, double value)
    {
        var aboveMin = entry.RangeFrom is null || value >= entry.RangeFrom.Value;
        var belowMax = entry.RangeTo is null || value <= entry.RangeTo.Value;

        return aboveMin && belowMax;
    }
}
