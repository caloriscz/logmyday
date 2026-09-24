using System.ComponentModel;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Mcp.Infrastructure;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace LogMyDay.Mcp.Tools;

/// <summary>
/// Units and quantities are shared by every user of the server, which is why changing them is
/// worded loudly and deleting one takes a sentinel.
/// </summary>
[McpServerToolType]
[Authorize(Policy = McpPolicies.Read)]
public sealed class UnitTools(IUnitService units)
{
    public const string DeleteSentinelPrefix = "DELETE_UNIT_";

    [McpServerTool(Name = "list_units", Title = "List units", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists all units (shared by all users) with their quantity and conversion to the base unit.")]
    public Task<IEnumerable<UnitResponse>> ListUnits() => units.GetAll();

    [McpServerTool(Name = "list_quantities", Title = "List quantities", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists the quantities (mass, volume, …) units belong to, each with its base unit.")]
    public Task<IEnumerable<QuantityResponse>> ListQuantities() => units.GetQuantities();

    [McpServerTool(Name = "get_unit", Title = "Get unit", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns one unit.")]
    public Task<UnitResponse> GetUnit([Description("Unit id.")] int unitId) => units.GetById(unitId);

    [McpServerTool(Name = "create_unit", Title = "Create unit", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Creates a unit visible to ALL users. Conversion is affine: value_in_base = aToBase * value + bToBase (the base unit itself is 1 and 0; e.g. °C → K is 1 and 273.15). The key must be unique within the quantity.")]
    public async Task<UnitResponse> CreateUnit(
        [Description("Machine key, e.g. \"mg\".")] string key,
        [Description("Display symbol, e.g. \"mg\".")] string symbol,
        [Description("Quantity id (list_quantities).")] int quantityId,
        [Description("Multiplier to the base unit.")] double aToBase = 1,
        [Description("Offset added after multiplying.")] double bToBase = 0,
        [Description("Decimals shown when displaying values.")] int decimals = 0)
    {
        var id = await units.CreateAsync(Request(key, symbol, quantityId, aToBase, bToBase, decimals));

        return await units.GetById(id);
    }

    [McpServerTool(Name = "update_unit", Title = "Update unit", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Updates a unit for ALL users; omitted arguments keep their current value.")]
    public async Task<UnitResponse> UpdateUnit(
        [Description("Unit id.")] int unitId,
        string? key = null,
        string? symbol = null,
        int? quantityId = null,
        double? aToBase = null,
        double? bToBase = null,
        int? decimals = null)
    {
        var current = await units.GetById(unitId);
        await units.Update(unitId, Request(
            key ?? current.Key,
            symbol ?? current.Symbol,
            quantityId ?? current.QuantityId,
            aToBase ?? current.AToBase,
            bToBase ?? current.BToBase,
            decimals ?? current.Decimals));

        return await units.GetById(unitId);
    }

    [McpServerTool(Name = "delete_unit", Title = "Delete unit", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = false)]
    [Authorize(Policy = McpPolicies.Write)]
    [Description("Deletes a unit for ALL users. Requires confirm = \"DELETE_UNIT_<unitId>\". Refused with conflict while any tag (of any user) uses it, and for a quantity's base unit.")]
    public async Task<object> DeleteUnit(
        [Description("Unit id.")] int unitId,
        [Description("Must be exactly DELETE_UNIT_<unitId>.")] string? confirm = null)
    {
        var unit = await units.GetById(unitId);
        ConfirmSentinel.Require(confirm, DeleteSentinelPrefix + unitId,
            $"Deletes unit {unitId} '{unit.Symbol}' ({unit.QuantityKey}) for every user of this server.");

        await units.Delete(unitId);

        return new { deleted = true, unitId, symbol = unit.Symbol };
    }

    private static UnitRequest Request(string key, string symbol, int quantityId, double aToBase, double bToBase, int decimals)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("key and symbol are required.");
        }

        if (aToBase == 0 || !double.IsFinite(aToBase) || !double.IsFinite(bToBase))
        {
            throw new ArgumentException("aToBase must be a non-zero number and bToBase a number.");
        }

        if (decimals is < 0 or > 10)
        {
            throw new ArgumentException("decimals must be between 0 and 10.");
        }

        return new UnitRequest { Key = key.Trim(), Symbol = symbol.Trim(), QuantityId = quantityId, AToBase = aToBase, BToBase = bToBase, Decimals = decimals };
    }
}
