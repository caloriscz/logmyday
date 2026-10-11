using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

/// <summary>A record type and its fields, in form order. Each field is a tag of its own; an
/// existing field is matched by <see cref="RecordFieldRequest.TagId"/>, a field without one is
/// created, and a field left out is removed with its values.</summary>
public class RecordTypeRequest
{
    public required string Name { get; set; }
    public int? GroupId { get; set; }
    public bool OncePerDay { get; set; }
    public List<RecordFieldRequest> Fields { get; set; } = new();
}

public class RecordFieldRequest
{
    /// <summary>The field's tag; null for a new field.</summary>
    public int? TagId { get; set; }

    public required string Name { get; set; }
    public string? Description { get; set; }
    public int TypeId { get; set; }
    public int? UnitId { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public double? Step { get; set; }
    public string? DefaultValue { get; set; }
    public int? OptionListId { get; set; }

    /// <summary>Sum, average or latest for a number field (average when left out); other fields
    /// always use latest.</summary>
    public DayCombine? DayCombine { get; set; }
}

public class RecordTypeResponse
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int? GroupId { get; set; }
    public string? GroupName { get; set; }
    public bool OncePerDay { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime DateCreated { get; set; }

    /// <summary>How many records of this type exist.</summary>
    public int RecordCount { get; set; }

    public List<RecordFieldResponse> Fields { get; set; } = new();
}

public class RecordFieldResponse
{
    public int TagId { get; set; }

    /// <summary>The field's name in its record type, e.g. "Systolic".</summary>
    public required string Name { get; set; }

    /// <summary>The field tag's full name, e.g. "Blood pressure: Systolic", as charts and
    /// statistics show it.</summary>
    public required string TagName { get; set; }

    public string? Description { get; set; }
    public int? InputTypeId { get; set; }
    public int? UnitId { get; set; }
    public string? UnitSymbol { get; set; }
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public double? Step { get; set; }
    public string? DefaultValue { get; set; }
    public int? OptionListId { get; set; }
    public DayCombine DayCombine { get; set; }
    public int Order { get; set; }

    /// <summary>How many values the field holds; removing the field deletes them.</summary>
    public int ValueCount { get; set; }
}
