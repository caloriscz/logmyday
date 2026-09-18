using System.ComponentModel;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>
/// Colour schemes map a tag's values to colours in the calendar and insights. An entry is a range
/// (open-ended when a bound is omitted) or an exact value; a Yes/No scheme is exact 1 + exact 0.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class ColorSchemeTools(McpUserContext user, IColorSchemeService schemes)
{
    public sealed record EntryInput(
        [property: Description("CSS colour, e.g. \"#16a34a\".")] string Color,
        [property: Description("Matches exactly this value (booleans: 1 = true, 0 = false). Mutually exclusive with rangeFrom/rangeTo.")] double? ExactValue = null,
        [property: Description("Lower bound, inclusive; omit for open-ended.")] double? RangeFrom = null,
        [property: Description("Upper bound, inclusive; omit for open-ended.")] double? RangeTo = null,
        [property: Description("Legend label.")] string? Label = null,
        [property: Description("Evaluation order; lower first. Defaults to the position in the list.")] int? SortOrder = null);

    [McpServerTool(Name = "list_color_schemes", Title = "List colour schemes", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists the user's colour schemes with their entries.")]
    public Task<IList<ColorSchemeResponse>> ListColorSchemes() => schemes.GetAll(user.UserId);

    [McpServerTool(Name = "get_color_scheme", Title = "Get colour scheme", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns one colour scheme with its entries. An entry whose rangeFrom equals rangeTo is an exact match.")]
    public Task<ColorSchemeResponse> GetColorScheme([Description("Colour scheme id.")] int colorSchemeId) => schemes.GetById(colorSchemeId, user.UserId);

    [McpServerTool(Name = "create_color_scheme", Title = "Create colour scheme", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Creates a colour scheme. Each entry is either exactValue or a rangeFrom/rangeTo pair (either bound may be omitted) plus a colour. For a Yes/No tag use exactValue 1 and exactValue 0.")]
    public async Task<ColorSchemeResponse> CreateColorScheme(
        [Description("Scheme name.")] string name,
        [Description("Entries, evaluated in order.")] List<EntryInput> entries,
        [Description("Optional description.")] string? description = null,
        [Description("Sort position among schemes.")] int? displayOrder = null)
    {
        var request = new ColorSchemeRequest { Name = Required(name), Description = description, DisplayOrder = displayOrder, Entries = ToRequests(entries) };

        var id = await schemes.Create(request, user.UserId);

        return await schemes.GetById(id, user.UserId);
    }

    [McpServerTool(Name = "update_color_scheme", Title = "Update colour scheme", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Updates a colour scheme; omitted arguments keep their current value. When entries is given it replaces all entries.")]
    public async Task<ColorSchemeResponse> UpdateColorScheme(
        [Description("Colour scheme id.")] int colorSchemeId,
        string? name = null,
        string? description = null,
        int? displayOrder = null,
        [Description("The complete new set of entries.")] List<EntryInput>? entries = null)
    {
        var current = await schemes.GetById(colorSchemeId, user.UserId);
        var request = new ColorSchemeRequest
        {
            Name = name == null ? current.Name : Required(name),
            Description = description ?? current.Description,
            DisplayOrder = displayOrder ?? current.DisplayOrder,
            Entries = entries == null
                ? current.Entries.Select(e => new ColorSchemeEntryRequest { Color = e.Color, RangeFrom = e.RangeFrom, RangeTo = e.RangeTo, Label = e.Label, SortOrder = e.SortOrder }).ToList()
                : ToRequests(entries)
        };

        await schemes.Update(colorSchemeId, request, user.UserId);

        return await schemes.GetById(colorSchemeId, user.UserId);
    }

    [McpServerTool(Name = "delete_color_scheme", Title = "Delete colour scheme", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Deletes a colour scheme. Tags that used it fall back to the default colours; nothing else changes.")]
    public async Task<object> DeleteColorScheme([Description("Colour scheme id.")] int colorSchemeId)
    {
        var scheme = await schemes.GetById(colorSchemeId, user.UserId);
        await schemes.Delete(colorSchemeId, user.UserId);

        return new { deleted = true, colorSchemeId, name = scheme.Name };
    }

    private static List<ColorSchemeEntryRequest> ToRequests(List<EntryInput> entries)
    {
        if (entries.Count == 0)
        {
            throw new ArgumentException("At least one entry is required.");
        }

        var requests = new List<ColorSchemeEntryRequest>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry.Color))
            {
                throw new ArgumentException($"Entry {i + 1} has no colour.");
            }

            if (entry.ExactValue.HasValue && (entry.RangeFrom.HasValue || entry.RangeTo.HasValue))
            {
                throw new ArgumentException($"Entry {i + 1}: use exactValue or rangeFrom/rangeTo, not both.");
            }

            if (entry.RangeFrom.HasValue && entry.RangeTo.HasValue && entry.RangeFrom > entry.RangeTo)
            {
                throw new ArgumentException($"Entry {i + 1}: rangeFrom must not exceed rangeTo.");
            }

            requests.Add(new ColorSchemeEntryRequest
            {
                Color = entry.Color.Trim(),
                RangeFrom = entry.ExactValue ?? entry.RangeFrom,
                RangeTo = entry.ExactValue ?? entry.RangeTo,
                Label = entry.Label,
                SortOrder = entry.SortOrder ?? i
            });
        }

        return requests;
    }

    private static string Required(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("The name is required.");
        }

        return name.Trim();
    }
}
