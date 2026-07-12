namespace LogMyDay.Domain.Helpers;

/// <summary>
/// Minimal shape the <see cref="ColorSchemeResolver"/> needs from a color-scheme entry. Implemented
/// by both the domain entity (<see cref="Entities.ColorSchemeEntry"/>) and the client-side response
/// DTO, so resolution works on either side without mapping.
/// </summary>
public interface IColorEntry
{
    double? RangeFrom { get; }
    double? RangeTo { get; }
    string Color { get; }
    int SortOrder { get; }
}
