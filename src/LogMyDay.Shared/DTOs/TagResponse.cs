using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class TagResponse
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public int? InputTypeId { get; set; }
    public int? TypeId { get; set; } // Add this property for tag type
    public bool IsRequired { get; set; } // Added for required column
    public bool IsRepeatable { get; set; }
    public TimeGranularity TimeGranularity { get; set; }
    public bool IsRange { get; set; }
    public int? UnitId { get; set; }
    public string? UnitSymbol { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public double? Step { get; set; }
    public string? DefaultValue { get; set; }
    public int? OptionListId { get; set; }
    public string? OptionListName { get; set; }
    public int? GroupId { get; set; }
    public string? GroupName { get; set; }
    public int? ColorSchemeId { get; set; }

    /// <summary>True while a Tag Activity Relations rule targets this tag: its values are
    /// generated and it is not offered for manual logging.</summary>
    public bool IsComputed { get; set; }

    /// <summary>The record type this tag is a field of: its values are written only as part of a
    /// record, so it is not offered for logging on its own. Analysis views read it like any tag.</summary>
    public int? RecordTypeId { get; set; }
    public string? RecordTypeName { get; set; }

    /// <summary>The title as analysis views show it: a computed tag is marked so a chart or
    /// comparison never passes a calculated value off as a logged one. Not serialized.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayTitle => IsComputed ? $"{Title} (computed)" : Title;

    /// <summary>Whether a value can be logged to this tag on its own: neither computed by a rule
    /// nor a field of a record type. Not serialized.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsLoggable => !IsComputed && RecordTypeId == null;
}
