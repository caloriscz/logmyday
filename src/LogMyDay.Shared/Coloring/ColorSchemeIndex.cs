using System.Globalization;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Helpers;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Shared.Coloring;

/// <summary>
/// Client-side lookup that resolves the display color for a tag value. Build one per page from the
/// user's color schemes, then call <see cref="ResolveColor"/> per rendered value. Falls back to the
/// input type's built-in default when the tag has no assigned scheme (see
/// <see cref="ColorSchemeResolver"/>).
/// </summary>
public sealed class ColorSchemeIndex
{
    public static readonly ColorSchemeIndex Empty = new(null);

    private readonly Dictionary<int, ColorSchemeResponse> _byId;

    public ColorSchemeIndex(IEnumerable<ColorSchemeResponse>? schemes)
    {
        _byId = schemes?.ToDictionary(s => s.Id) ?? new Dictionary<int, ColorSchemeResponse>();
    }

    public string? ResolveColor(int? inputTypeId, int? colorSchemeId, string? value)
    {
        if (inputTypeId is not int typeId)
        {
            return null;
        }

        if (!TryReadValue(typeId, value, out var number))
        {
            return null;
        }

        IReadOnlyList<ColorSchemeEntryResponse>? entries = null;
        if (colorSchemeId is int id && _byId.TryGetValue(id, out var scheme))
        {
            entries = scheme.Entries;
        }

        return ColorSchemeResolver.Resolve(typeId, entries, number);
    }

    /// <summary>
    /// A Boolean tag stores "true"/"false"; its scheme is written against 1 and 0. Reading the
    /// words here means every surface that hands the raw activity value to this index colours
    /// booleans the same way, rather than only the ones that pre-convert. The mapping is by tag
    /// type, so a String tag holding the word "true" still resolves to nothing.
    /// </summary>
    private static bool TryReadValue(int typeId, string? value, out double number)
    {
        if (typeId == InputTypeIds.Boolean && bool.TryParse(value, out var flag))
        {
            number = flag ? 1 : 0;

            return true;
        }

        return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out number);
    }
}
