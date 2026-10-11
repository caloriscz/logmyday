namespace LogMyDay.Shared.DTOs;

/// <summary>One record: the time it was measured and a value per field. Empty values are not
/// stored; a record needs at least one value.</summary>
public class RecordRequest
{
    public int RecordTypeId { get; set; }
    public DateTime DateStarted { get; set; }
    public List<RecordValueRequest> Values { get; set; } = new();
}

public class RecordValueRequest
{
    /// <summary>The field's tag.</summary>
    public int TagId { get; set; }

    /// <summary>The value, encoded as for an activity of the field's type.</summary>
    public string? Value { get; set; }
}

public class RecordResponse
{
    public int Id { get; set; }
    public int RecordTypeId { get; set; }
    public string RecordTypeName { get; set; } = string.Empty;
    public DateTime DateStarted { get; set; }
    public DateTime DateCreated { get; set; }

    /// <summary>The stored values, in the record type's field order.</summary>
    public List<RecordValueResponse> Values { get; set; } = new();
}

public class RecordValueResponse
{
    public int ActivityId { get; set; }
    public int TagId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public int? InputTypeId { get; set; }
    public string? UnitSymbol { get; set; }
    public string Value { get; set; } = string.Empty;
}
