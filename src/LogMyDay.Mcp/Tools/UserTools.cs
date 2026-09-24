using System.ComponentModel;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp.Contracts;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Preferences;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>
/// The key's own user. Preferences can be changed; the e-mail and password cannot — a key must not
/// be able to rotate the credentials that guard key creation.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class UserTools(McpUserContext user, IUserService users, IApiKeyService keys)
{
    [McpServerTool(Name = "get_me", Title = "Get me", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("The user this key acts as (e-mail, display name, admin flag, culture, time zone, display preferences) and the key itself (name, prefix, scope, expiry).")]
    public async Task<object> GetMe(CancellationToken cancellationToken)
    {
        var account = await users.Get(user.UserId, cancellationToken) ?? throw new KeyNotFoundException("User not found");
        var key = (await keys.List(user.UserId, cancellationToken)).FirstOrDefault(k => k.Id == user.ApiKeyId);

        return new
        {
            user = UserMapping.ToDto(account),
            apiKey = key == null ? null : new { key.Name, key.Prefix, key.Scope, key.ExpiresUtc, key.LastUsedUtc }
        };
    }

    [McpServerTool(Name = "update_my_preferences", Title = "Update my preferences", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Updates the user's display name, culture (e.g. en-US, de-DE — decides number/date formatting and the first day of the week), IANA time zone (e.g. Europe/Vienna) and activity display preferences. Omitted arguments keep their value. E-mail and password cannot be changed through a key.")]
    public async Task<UserDto> UpdateMyPreferences(
        [Description("Display name; \"\" clears it.")] string? displayName = null,
        [Description("Culture name such as en-US.")] string? culture = null,
        [Description("IANA time zone id such as Europe/Vienna.")] string? timeZone = null,
        [Description("daily, weekly or monthly.")] string? activityDisplayType = null,
        [Description("desc, asc, group-asc or group-desc.")] string? activitySortOrder = null,
        [Description("desc or asc.")] string? activityPeriodSort = null,
        CancellationToken cancellationToken = default)
    {
        var updated = await users.Update(
            user.UserId,
            email: null,
            displayName: displayName == null ? null : (displayName.Trim().Length == 0 ? string.Empty : displayName.Trim()),
            isAdmin: null,
            culture: culture == null ? null : UserMapping.RequireCulture(culture),
            timeZone: timeZone == null ? null : UserMapping.RequireTimeZone(timeZone),
            actorId: user.UserId,
            cancellationToken,
            activityDisplayType: activityDisplayType == null ? null : RequireChoice(activityDisplayType, "activityDisplayType",
                ActivityFilterPreferences.DailyDisplayType, ActivityFilterPreferences.WeeklyDisplayType, ActivityFilterPreferences.MonthlyDisplayType),
            activitySortOrder: activitySortOrder == null ? null : RequireChoice(activitySortOrder, "activitySortOrder",
                ActivityFilterPreferences.DescSortOrder, ActivityFilterPreferences.AscSortOrder, ActivityFilterPreferences.GroupAscSortOrder, ActivityFilterPreferences.GroupDescSortOrder),
            activityPeriodSort: activityPeriodSort == null ? null : RequireChoice(activityPeriodSort, "activityPeriodSort",
                ActivityFilterPreferences.DescSortOrder, ActivityFilterPreferences.AscSortOrder));

        return UserMapping.ToDto(updated);
    }

    private static string RequireChoice(string value, string argument, params string[] allowed)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (!allowed.Contains(normalized))
        {
            throw new ArgumentException($"{argument} must be one of: {string.Join(", ", allowed)}.");
        }

        return normalized;
    }
}
