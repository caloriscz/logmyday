using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface IDashboardService
{
    Task<IList<DashboardResponse>> GetDashboards(Guid userId);
    Task<DashboardResponse> GetDashboard(int id, Guid userId);
    Task<DashboardResponse> GetOrCreateDefault(Guid userId);
    Task<DashboardResponse> Create(DashboardRequest request, Guid userId);
    Task Update(int id, DashboardRequest request, Guid userId);
    Task Delete(int id, Guid userId);
    Task<IList<DashboardPanelResponse>> GetPanels(int dashboardId, Guid userId);
    Task<DashboardPanelResponse> AddPanel(int dashboardId, DashboardPanelRequest request, Guid userId);
    Task UpdatePanel(int dashboardId, int panelId, DashboardPanelRequest request, Guid userId);
    Task RemovePanel(int dashboardId, int panelId, Guid userId);
    Task ReorderPanels(int dashboardId, List<PanelReorderRequest> request, Guid userId);
    Task<DashboardDataResponse> GetDashboardData(int dashboardId, Guid userId);
}
