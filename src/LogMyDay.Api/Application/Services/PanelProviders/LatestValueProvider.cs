using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LogMyDay.Api.Application.Services.PanelProviders;

public class LatestValueProvider : IPanelDataProvider
{
    private readonly LogMyDayDbContext _context;

    public LatestValueProvider(LogMyDayDbContext context)
    {
        _context = context;
    }

    public bool CanHandle(DashboardPanel panel) =>
        panel.WidgetTypeId == WidgetTypeIds.LatestValue;

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
        var tomorrow = today.AddDays(1);

        var activity = await _context.Activities
            .AsNoTracking()
            .Where(a => a.UserId == userId
                && a.TagId == panel.TagId.Value
                && a.DateStarted >= today
                && a.DateStarted < tomorrow)
            .OrderByDescending(a => a.DateStarted)
            .FirstOrDefaultAsync();

        if (activity == null)
        {
            return data;
        }

        if (PanelValueFormatter.TryParseDecimal(activity.Description, out var value))
        {
            data.HasData = true;
            data.NumericValue = value;
            data.DisplayValue = PanelValueFormatter.Format(value, panel.Tag?.Unit?.Symbol);
        }
        else if (!string.IsNullOrWhiteSpace(activity.Description))
        {
            data.HasData = true;
            data.DisplayValue = activity.Description;
        }

        return data;
    }
}
