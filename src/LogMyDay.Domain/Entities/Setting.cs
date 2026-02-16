namespace LogMyDay.Domain.Entities;

/// <summary>
/// Represents an application setting stored in the database.
/// </summary>
public sealed class Setting
{
    /// <summary>
    /// The unique key for the setting (e.g., "AI:Enabled", "AI:Model").
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// The setting value, stored as a string.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Optional description of what this setting controls.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When the setting was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the setting was last updated.
    /// </summary>
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
