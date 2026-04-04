using LogMyDay.Shared.DTOs;
using Refit;

namespace LogMyDay.Shared.Interfaces;

public interface IDashboardApi
{
    [Get("/api/dashboards")]
    Task<List<DashboardResponse>> GetDashboards();

    [Get("/api/dashboards/{id}")]
    Task<DashboardResponse> GetDashboard(int id);

    [Get("/api/dashboards/default")]
    Task<DashboardResponse> GetOrCreateDefault();

    [Post("/api/dashboards")]
    Task<DashboardResponse> Create([Body] DashboardRequest request);

    [Put("/api/dashboards/{id}")]
    Task Update(int id, [Body] DashboardRequest request);

    [Delete("/api/dashboards/{id}")]
    Task Delete(int id);

    [Get("/api/dashboards/{dashboardId}/panels")]
    Task<List<DashboardPanelResponse>> GetPanels(int dashboardId);

    [Post("/api/dashboards/{dashboardId}/panels")]
    Task<DashboardPanelResponse> AddPanel(int dashboardId, [Body] DashboardPanelRequest request);

    [Put("/api/dashboards/{dashboardId}/panels/{panelId}")]
    Task UpdatePanel(int dashboardId, int panelId, [Body] DashboardPanelRequest request);

    [Delete("/api/dashboards/{dashboardId}/panels/{panelId}")]
    Task RemovePanel(int dashboardId, int panelId);

    [Put("/api/dashboards/{dashboardId}/panels/reorder")]
    Task ReorderPanels(int dashboardId, [Body] List<PanelReorderRequest> request);

    [Get("/api/dashboards/{dashboardId}/data")]
    Task<DashboardDataResponse> GetDashboardData(int dashboardId);
}
