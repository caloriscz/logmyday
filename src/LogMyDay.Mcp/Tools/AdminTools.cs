using System.ComponentModel;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Options;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp.Contracts;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.DTOs.Settings;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>
/// Server administration for admin users holding a read-write key. Only prefixes and masked keys
/// ever leave; the audit filter redacts password and API-key arguments.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Admin)]
public sealed class AdminTools(McpUserContext user, IUserService users, ISettingsService settings)
{
    public const string DeleteSentinelPrefix = "DELETE_USER_";

    [McpServerTool(Name = "admin_list_users", Title = "List users (admin)", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Admin: every user account on this server with culture, time zone and preferences. No credentials.")]
    public async Task<List<UserDto>> AdminListUsers(CancellationToken cancellationToken)
    {
        return (await users.List(cancellationToken)).Select(UserMapping.ToDto).ToList();
    }

    [McpServerTool(Name = "admin_create_user", Title = "Create user (admin)", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Admin)]
    [Description("Admin: creates a user account. The password (10+ characters) is never echoed or audited; the user should change it after first login.")]
    public async Task<UserDto> AdminCreateUser(
        [Description("E-mail, unique.")] string email,
        [Description("Initial password, at least 10 characters.")] string password,
        [Description("Display name.")] string? displayName = null,
        [Description("Grant admin rights.")] bool isAdmin = false,
        [Description("Culture such as en-US.")] string culture = "en-US",
        [Description("IANA time zone such as Europe/Vienna.")] string timeZone = "Europe/Vienna",
        CancellationToken cancellationToken = default)
    {
        var created = await users.CreateUser(
            UserMapping.RequireEmail(email),
            UserMapping.RequirePassword(password, "password"),
            displayName,
            isAdmin,
            UserMapping.RequireCulture(culture),
            UserMapping.RequireTimeZone(timeZone),
            user.UserId,
            cancellationToken);

        return UserMapping.ToDto(created);
    }

    [McpServerTool(Name = "admin_update_user", Title = "Update user (admin)", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Admin)]
    [Description("Admin: updates another user's e-mail, display name, admin flag, culture or time zone; omitted arguments keep their value.")]
    public async Task<UserDto> AdminUpdateUser(
        [Description("User id.")] Guid userId,
        string? email = null,
        [Description("\"\" clears it.")] string? displayName = null,
        bool? isAdmin = null,
        string? culture = null,
        string? timeZone = null,
        CancellationToken cancellationToken = default)
    {
        _ = await users.Get(userId, cancellationToken) ?? throw new KeyNotFoundException("User not found");

        var updated = await users.Update(
            userId,
            email == null ? null : UserMapping.RequireEmail(email),
            displayName == null ? null : displayName.Trim(),
            isAdmin,
            culture == null ? null : UserMapping.RequireCulture(culture),
            timeZone == null ? null : UserMapping.RequireTimeZone(timeZone),
            user.UserId,
            cancellationToken);

        return UserMapping.ToDto(updated);
    }

    [McpServerTool(Name = "admin_delete_user", Title = "Delete user (admin)", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Admin)]
    [Description("Admin: deletes a user account and EVERYTHING it owns — activities, tags, reminders, todo lists, API keys. Requires confirm = \"DELETE_USER_<email>\" with that user's exact e-mail. You cannot delete yourself.")]
    public async Task<object> AdminDeleteUser(
        [Description("User id.")] Guid userId,
        [Description("Must be exactly DELETE_USER_<email>.")] string? confirm = null,
        CancellationToken cancellationToken = default)
    {
        var target = await users.Get(userId, cancellationToken) ?? throw new KeyNotFoundException("User not found");
        ConfirmSentinel.Require(confirm, DeleteSentinelPrefix + target.Email,
            $"Deletes the account {target.Email} ({target.Id}) and all of its data and API keys.");

        await users.Delete(userId, user.UserId, cancellationToken);

        return new { deleted = true, userId, email = target.Email };
    }

    [McpServerTool(Name = "admin_reset_password", Title = "Reset password (admin)", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Admin)]
    [Description("Admin: sets a new password (10+ characters) for a user without knowing the old one. The new password is never echoed or audited. Their API keys stay valid.")]
    public async Task<object> AdminResetPassword(
        [Description("User id.")] Guid userId,
        [Description("New password, at least 10 characters.")] string newPassword,
        CancellationToken cancellationToken = default)
    {
        _ = await users.Get(userId, cancellationToken) ?? throw new KeyNotFoundException("User not found");
        await users.AdminResetPassword(userId, UserMapping.RequirePassword(newPassword, "newPassword"), user.UserId, cancellationToken);

        return new { reset = true, userId };
    }

    [McpServerTool(Name = "admin_get_ai_settings", Title = "Get AI settings (admin)", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Admin: the in-app AI assistant's configuration. The provider API key is masked to its last four characters.")]
    public async Task<AiSettingsDto> AdminGetAiSettings(CancellationToken cancellationToken)
    {
        var options = await settings.GetAiOptionsAsync(cancellationToken);

        return new AiSettingsDto(options.Enabled, options.Provider, options.Model, MaskApiKey(options.ApiKey),
            options.MaxTokens, options.Temperature, options.MaxConversationMessages);
    }

    [McpServerTool(Name = "admin_update_ai_settings", Title = "Update AI settings (admin)", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Admin)]
    [Description("Admin: changes the in-app AI assistant's configuration; omitted arguments keep their value. A null or empty apiKey keeps the stored key; the key is never echoed or audited.")]
    public async Task<AiSettingsDto> AdminUpdateAiSettings(
        bool? enabled = null,
        [Description("Provider name, e.g. openai.")] string? provider = null,
        [Description("Model name, e.g. gpt-4o-mini.")] string? model = null,
        [Description("New provider API key; omit to keep the current one.")] string? apiKey = null,
        int? maxTokens = null,
        float? temperature = null,
        int? maxConversationMessages = null,
        CancellationToken cancellationToken = default)
    {
        var current = await settings.GetAiOptionsAsync(cancellationToken);
        if (maxTokens is < 1 || maxConversationMessages is < 1)
        {
            throw new ArgumentException("maxTokens and maxConversationMessages must be positive.");
        }

        if (temperature is < 0 or > 2)
        {
            throw new ArgumentException("temperature must be between 0 and 2.");
        }

        var updated = new AiOptions
        {
            Enabled = enabled ?? current.Enabled,
            Provider = string.IsNullOrWhiteSpace(provider) ? current.Provider : provider.Trim(),
            Model = string.IsNullOrWhiteSpace(model) ? current.Model : model.Trim(),
            ApiKey = string.IsNullOrWhiteSpace(apiKey) ? current.ApiKey : apiKey.Trim(),
            MaxTokens = maxTokens ?? current.MaxTokens,
            Temperature = temperature ?? current.Temperature,
            MaxConversationMessages = maxConversationMessages ?? current.MaxConversationMessages
        };

        await settings.UpdateAiOptionsAsync(updated, cancellationToken);

        return await AdminGetAiSettings(cancellationToken);
    }

    /// <summary>The same masking the Settings page uses: everything but the last four characters.</summary>
    public static string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return string.Empty;
        }

        return apiKey.Length <= 4 ? "****" : $"**************{apiKey[^4..]}";
    }
}
