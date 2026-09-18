using System.ComponentModel;
using System.Text.Json;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp.Contracts;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Resources;

/// <summary>
/// The user's reference data as readable resources, for clients that attach context rather than
/// call tools. Everything is user-scoped JSON in the same shape the tools return.
/// </summary>
[McpServerResourceType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class LogMyDayResources(
    McpUserContext user,
    ITagService tags,
    ITagGroupService groups,
    ITagOptionListService optionLists,
    IUnitService units,
    IInputTypeService inputTypes,
    IUserService users)
{
    public const string Scheme = "logmyday://";
    public const string JsonMime = "application/json";

    [McpServerResource(UriTemplate = "logmyday://tags", Name = "tags", Title = "Tags", MimeType = JsonMime)]
    [Description("All of the user's tags as compact summaries (id, title, group, input type, unit, repeatability).")]
    public async Task<TextResourceContents> Tags()
    {
        var names = await InputTypeNames();
        var all = await tags.GetAll(user.UserId);

        return Json("logmyday://tags", all.Select(t => TagSummary.From(t, names)).OrderBy(t => t.Title).ToList());
    }

    [McpServerResource(UriTemplate = "logmyday://tags/{id}", Name = "tag", Title = "Tag", MimeType = JsonMime)]
    [Description("One tag's full configuration, with its value-encoding rule and options.")]
    public async Task<TextResourceContents> Tag(int id)
    {
        var tag = await tags.GetTagById(id, user.UserId);
        var options = tag.OptionListId is int listId ? await optionLists.GetById(listId, user.UserId) : null;

        return Json($"logmyday://tags/{id}", new
        {
            tag,
            valueEncoding = ValueEncoder.Describe(tag.TypeId),
            options = options?.Options.Select(o => new { o.Value, o.DisplayName }).ToList()
        });
    }

    [McpServerResource(UriTemplate = "logmyday://tag-groups", Name = "tag-groups", Title = "Tag groups", MimeType = JsonMime)]
    [Description("The user's tag groups.")]
    public async Task<TextResourceContents> TagGroups() => Json("logmyday://tag-groups", await groups.GetAll(user.UserId));

    [McpServerResource(UriTemplate = "logmyday://option-lists", Name = "option-lists", Title = "Option lists", MimeType = JsonMime)]
    [Description("Option lists the user can use, with their options.")]
    public async Task<TextResourceContents> OptionLists() => Json("logmyday://option-lists", await optionLists.GetAll(user.UserId));

    [McpServerResource(UriTemplate = "logmyday://units", Name = "units", Title = "Units", MimeType = JsonMime)]
    [Description("All units and quantities (shared by every user).")]
    public async Task<TextResourceContents> Units()
    {
        return Json("logmyday://units", new { units = await units.GetAll(), quantities = await units.GetQuantities() });
    }

    [McpServerResource(UriTemplate = "logmyday://input-types", Name = "input-types", Title = "Input types", MimeType = JsonMime)]
    [Description("The input types a tag can have, each with the rule for encoding a value.")]
    public async Task<TextResourceContents> InputTypes()
    {
        var types = await inputTypes.GetAllInputTypes();

        return Json("logmyday://input-types", types.Select(t => new
        {
            t.Id,
            t.Name,
            t.Description,
            t.IsRangeEditable,
            t.IsMinimumEditable,
            t.IsMaximumEditable,
            t.IsStepEditable,
            t.IsRepeatableEditable,
            ValueEncoding = ValueEncoder.Describe(t.Id)
        }).ToList());
    }

    [McpServerResource(UriTemplate = "logmyday://me", Name = "me", Title = "Current user", MimeType = JsonMime)]
    [Description("The user the key acts as: email, display name, admin flag, culture, time zone and display preferences.")]
    public async Task<TextResourceContents> Me()
    {
        var account = await users.Get(user.UserId, CancellationToken.None) ?? throw new KeyNotFoundException("User not found");

        return Json("logmyday://me", new CurrentUserDto(
            account.Id,
            account.Email,
            account.DisplayName,
            account.IsAdmin,
            account.Culture,
            account.TimeZone,
            account.ActivityDisplayType,
            account.ActivitySortOrder,
            account.ActivityPeriodSort));
    }

    private async Task<IReadOnlyDictionary<int, string>> InputTypeNames()
    {
        return (await inputTypes.GetAllInputTypes()).ToDictionary(t => t.Id, t => t.Name);
    }

    private static TextResourceContents Json(string uri, object payload) => new()
    {
        Uri = uri,
        MimeType = JsonMime,
        Text = JsonSerializer.Serialize(payload, McpJson.Options)
    };
}
