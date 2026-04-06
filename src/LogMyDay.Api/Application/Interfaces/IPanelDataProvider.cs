using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IPanelDataProvider
{
    bool CanHandle(DashboardPanel panel);
    Task<PanelDataResponse> GetData(DashboardPanel panel, Guid userId);
}
