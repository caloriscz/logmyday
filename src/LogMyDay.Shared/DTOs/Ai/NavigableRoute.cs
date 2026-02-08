using LogMyDay.Shared.Attributes;

namespace LogMyDay.Shared.DTOs.Ai;

/// <summary>
/// Represents a user-navigable route in the application.
/// </summary>
public sealed record NavigableRoute(
    string Path,
    string Label,
    string Description,
    bool RequiresAdmin = false,
    ClientContext ClientContext = ClientContext.All
);
