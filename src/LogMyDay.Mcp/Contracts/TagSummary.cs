using LogMyDay.Domain.Enums;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Mcp.Contracts;

/// <summary>
/// The compact row list tools and lookups return: enough to pick a tag and encode a value for it,
/// without the full configuration <c>get_tag</c> gives.
/// </summary>
public sealed record TagSummary(
    int Id,
    string Title,
    string Name,
    int? GroupId,
    string? GroupName,
    int? InputTypeId,
    string? InputType,
    string? UnitSymbol,
    bool IsRepeatable,
    bool IsRequired,
    TimeGranularity TimeGranularity,
    int? OptionListId)
{
    public static TagSummary From(TagResponse tag, IReadOnlyDictionary<int, string> inputTypeNames)
    {
        var name = tag.GroupName != null && tag.Title.StartsWith(tag.GroupName + ": ", StringComparison.Ordinal)
            ? tag.Title[(tag.GroupName.Length + 2)..]
            : tag.Title;
        var title = tag.GroupName != null ? $"{tag.GroupName}: {name}" : name;

        return new TagSummary(
            tag.Id,
            title,
            name,
            tag.GroupId,
            tag.GroupName,
            tag.TypeId,
            tag.TypeId is int typeId && inputTypeNames.TryGetValue(typeId, out var typeName) ? typeName : null,
            tag.UnitSymbol,
            tag.IsRepeatable,
            tag.IsRequired,
            tag.TimeGranularity,
            tag.OptionListId);
    }
}
