using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Application.Services.PanelProviders;

/// <summary>
/// Panel Widget: Monthly Min/Max
/// Computes the minimum and maximum of the latest recorded value per day over the last 30 days (including today).
/// Applies to: MinMaxValue panel type + Last30Days time range.
/// DisplayValue holds the minimum; SecondaryDisplayValue holds the maximum.
/// </summary>
public class MonthlyMinMaxProvider : IPanelDataProvider
{
    private readonly LogMyDayDbContext _context;

    public MonthlyMinMaxProvider(LogMyDayDbContext context)
    {
        _context = context;
    }

    public bool CanHandle(DashboardPanel panel) =>
        panel.WidgetTypeId == WidgetTypeIds.MonthlyMinMax;

    public async Task<PanelDataResponse> GetData(DashboardPanel panel, Guid userId)
    {
        var data = new PanelDataResponse
        {
            PanelId = panel.Id,
            WidgetTypeId = panel.WidgetTypeId,
            TagName = panel.Tag?.TagName,
            UnitSymbol = panel.Tag?.Unit?.Symbol
        };

        if (!panel.TagId.HasValue)
        {
            return data;
        }

        var today = DateTime.UtcNow.Date;
        var windowStart = today.AddDays(-29); // 30 days total including today
        var tomorrow = today.AddDays(1);

        var activities = await _context.Activities
            .AsNoTracking()
            .Where(a => a.UserId == userId
                && a.TagId == panel.TagId.Value
                && a.DateStarted >= windowStart
                && a.DateStarted < tomorrow)
            .OrderByDescending(a => a.DateStarted)
            .ToListAsync();

        // Take the latest activity per calendar day, then compute min and max
        var dailyValues = activities
            .GroupBy(a => a.DateStarted.Date)
            .Select(g => g.First()) // latest per day (already ordered desc)
            .Select(a => PanelValueFormatter.TryParseDecimal(a.Description, out var v) ? (decimal?)v : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        if (dailyValues.Count == 0)
        {
            return data;
        }

        var min = dailyValues.Min();
        var max = dailyValues.Max();
        var unitSymbol = panel.Tag?.Unit?.Symbol;

        data.HasData = true;
        data.NumericValue = min;
        data.DisplayValue = PanelValueFormatter.Format(min, unitSymbol);
        data.SecondaryNumericValue = max;
        data.SecondaryDisplayValue = PanelValueFormatter.Format(max, unitSymbol);

        return data;
    }
}
