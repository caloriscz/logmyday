using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Services.PanelProviders;

public class PanelDataProviderFactory
{
    private readonly IEnumerable<IPanelDataProvider> _providers;

    public PanelDataProviderFactory(IEnumerable<IPanelDataProvider> providers)
    {
        _providers = providers;
    }

    public async Task<PanelDataResponse> GetData(DashboardPanel panel, Guid userId)
    {
        var provider = _providers.FirstOrDefault(p => p.CanHandle(panel));

        if (provider == null)
        {
            return new PanelDataResponse
            {
                PanelId = panel.Id,
                WidgetTypeId = panel.WidgetTypeId,
                TagName = panel.Tag?.TagName,
                UnitSymbol = panel.Tag?.Unit?.Symbol,
                HasData = false
            };
        }

        return await provider.GetData(panel, userId);
    }
}
