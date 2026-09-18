using System.ComponentModel;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Domain.Enums;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>Scan mappings tie a barcode or QR code to a tag so the mobile app can log by scanning.</summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class ScanMappingTools(McpUserContext user, IScanMappingService mappings)
{
    [McpServerTool(Name = "list_scan_mappings", Title = "List scan mappings", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists the user's barcode/QR-code to tag mappings.")]
    public Task<IList<ScanMappingResponse>> ListScanMappings() => mappings.GetAll(user.UserId);

    [McpServerTool(Name = "get_scan_mapping", Title = "Get scan mapping", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns one scan mapping.")]
    public Task<ScanMappingResponse> GetScanMapping([Description("Mapping id.")] int mappingId) => mappings.GetById(mappingId, user.UserId);

    [McpServerTool(Name = "lookup_scan_code", Title = "Look up scan code", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Resolves a scanned code value to its mapping and tag; found is false when nothing matches.")]
    public Task<ScanLookupResponse> LookupScanCode([Description("The code as scanned.")] string codeValue)
    {
        return mappings.Lookup(RequireCode(codeValue), user.UserId);
    }

    [McpServerTool(Name = "create_scan_mapping", Title = "Create scan mapping", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Maps a code to one of the user's tags. A code can be mapped only once per user (conflict otherwise). defaultDescription is the value pre-filled when the code is scanned.")]
    public Task<ScanMappingResponse> CreateScanMapping(
        [Description("The code value.")] string codeValue,
        [Description("Tag id (must belong to the user).")] int tagId,
        [Description("Barcode (default) or QRCode.")] CodeType codeType = CodeType.Barcode,
        [Description("Friendly name shown after scanning.")] string? displayName = null,
        [Description("Value pre-filled when scanned, encoded for the tag's input type.")] string? defaultDescription = null,
        [Description("Inactive mappings are ignored by lookups.")] bool isActive = true)
    {
        var request = new ScanMappingRequest
        {
            CodeValue = RequireCode(codeValue),
            CodeType = codeType,
            TagId = tagId,
            DisplayName = displayName,
            DefaultDescription = defaultDescription,
            IsActive = isActive
        };

        return mappings.Create(request, user.UserId);
    }

    [McpServerTool(Name = "update_scan_mapping", Title = "Update scan mapping", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Updates a scan mapping; omitted arguments keep their current value. Pass \"\" for displayName or defaultDescription to clear them.")]
    public async Task<ScanMappingResponse> UpdateScanMapping(
        [Description("Mapping id.")] int mappingId,
        string? codeValue = null,
        int? tagId = null,
        CodeType? codeType = null,
        [Description("Or \"\" to clear.")] string? displayName = null,
        [Description("Or \"\" to clear.")] string? defaultDescription = null,
        bool? isActive = null)
    {
        var current = await mappings.GetById(mappingId, user.UserId);
        var request = new ScanMappingRequest
        {
            CodeValue = codeValue == null ? current.CodeValue : RequireCode(codeValue),
            CodeType = codeType ?? current.CodeType,
            TagId = tagId ?? current.TagId,
            DisplayName = displayName switch { null => current.DisplayName, "" => null, _ => displayName },
            DefaultDescription = defaultDescription switch { null => current.DefaultDescription, "" => null, _ => defaultDescription },
            IsActive = isActive ?? current.IsActive
        };

        return await mappings.Update(mappingId, request, user.UserId);
    }

    [McpServerTool(Name = "delete_scan_mapping", Title = "Delete scan mapping", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Deletes a scan mapping. The tag and its activities are untouched.")]
    public async Task<object> DeleteScanMapping([Description("Mapping id.")] int mappingId)
    {
        var mapping = await mappings.GetById(mappingId, user.UserId);
        await mappings.Delete(mappingId, user.UserId);

        return new { deleted = true, mappingId, codeValue = mapping.CodeValue };
    }

    private static string RequireCode(string codeValue)
    {
        if (string.IsNullOrWhiteSpace(codeValue))
        {
            throw new ArgumentException("codeValue is required.");
        }

        return codeValue.Trim();
    }
}
