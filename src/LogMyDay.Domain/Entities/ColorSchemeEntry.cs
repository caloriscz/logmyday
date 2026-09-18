using System.ComponentModel.DataAnnotations.Schema;
using LogMyDay.Domain.Helpers;

namespace LogMyDay.Domain.Entities;

/// <summary>
/// One value→color mapping within a <see cref="ColorScheme"/>. A single storage shape covers both
/// exact values and ranges: an exact value is <c>RangeFrom == RangeTo</c>; a band uses distinct
/// bounds; a null bound is open-ended (e.g. <c>RangeFrom = 90, RangeTo = null</c> matches ≥ 90).
/// </summary>
public class ColorSchemeEntry : IColorEntry
{
    public int Id { get; set; }

    public int ColorSchemeId { get; set; }

    [ForeignKey(nameof(ColorSchemeId))]
    public ColorScheme? ColorScheme { get; set; }

    public double? RangeFrom { get; set; }
    public double? RangeTo { get; set; }

    /// <summary>Hex color, e.g. <c>#22c55e</c>.</summary>
    public required string Color { get; set; }

    public int SortOrder { get; set; }

    public string? Label { get; set; }
}
