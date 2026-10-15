namespace LogMyDay.Shared.DTOs;

/// <summary>A record type with its fields and records. Fields are defined here rather than in the
/// backup's tag list, and records carry their values by field name, so a record type restores as
/// one piece: its records are never merged into activity streaks.</summary>
public class RecordTypeBackup
{
    public string Name { get; set; } = string.Empty;
    public string? GroupName { get; set; }
    public bool OncePerDay { get; set; }
    public List<RecordFieldBackup> Fields { get; set; } = new();
    public List<RecordBackup> Records { get; set; } = new();
}

public class RecordFieldBackup
{
    /// <summary>The field's name within its record type; its tag is "{record type}: {field}".</summary>
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? InputTypeName { get; set; }
    public string? UnitKey { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public double? Step { get; set; }
    public string? DefaultValue { get; set; }
    public string? OptionListKey { get; set; }
}

public class RecordBackup
{
    public DateTime DateStarted { get; set; }
    public DateTime DateCreated { get; set; }
    public List<RecordValueBackup> Values { get; set; } = new();
}

public class RecordValueBackup
{
    public string FieldName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
