using System.Globalization;
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

        if (!double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
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
}
