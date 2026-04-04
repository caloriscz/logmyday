using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure.Data;
using LogMyDay.Domain.Constants;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly LogMyDayDbContext _context;
    private readonly ILogger<DashboardService> _logger;

    private static readonly HashSet<int> ValidPanelTypeIds = new()
    {
        DashboardPanelTypeIds.NumericValue
    };

    private static readonly HashSet<int> ValidAggregationTypeIds = new()
    {
        AggregationTypeIds.LatestValue
    };

    private static readonly HashSet<int> ValidTimeRangeIds = new()
    {
        TimeRangeIds.Today
    };

    private static readonly HashSet<int> ValidSizeIds = new()
    {
        PanelSizeIds.Small,
        PanelSizeIds.Wide
    };

    private static readonly HashSet<int> NumericInputTypeIds = new()
    {
        InputTypeIds.Integer,
        InputTypeIds.Decimal,
        InputTypeIds.StarRating,
        InputTypeIds.StarRating10,
        InputTypeIds.Percentage,
        InputTypeIds.Score,
        InputTypeIds.Score10
    };

    public DashboardService(LogMyDayDbContext context, ILogger<DashboardService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IList<DashboardResponse>> GetDashboards(Guid userId)
    {
        var dashboards = await _context.Dashboards
            .AsNoTracking()
            .Include(d => d.Panels.OrderBy(p => p.DisplayOrder))
                .ThenInclude(p => p.Tag)
                    .ThenInclude(t => t!.Unit)
            .Where(d => d.UserId == userId)
            .OrderBy(d => d.Id)
            .ToListAsync();

        return dashboards.Select(MapToResponse).ToList();
    }

    public async Task<DashboardResponse> GetDashboard(int id, Guid userId)
    {
        var dashboard = await GetDashboardEntity(id, userId);

        return MapToResponse(dashboard);
    }

    public async Task<DashboardResponse> GetOrCreateDefault(Guid userId)
    {
        var dashboard = await _context.Dashboards
            .Include(d => d.Panels.OrderBy(p => p.DisplayOrder))
                .ThenInclude(p => p.Tag)
                    .ThenInclude(t => t!.Unit)
            .FirstOrDefaultAsync(d => d.UserId == userId && d.IsDefault);

        if (dashboard == null)
        {
            dashboard = new Dashboard
            {
                UserId = userId,
                Name = "My Dashboard",
                IsDefault = true,
                DateCreated = DateTime.UtcNow
            };

            _context.Dashboards.Add(dashboard);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created default dashboard {DashboardId} for user {UserId}", dashboard.Id, userId);
        }

        return MapToResponse(dashboard);
    }

    public async Task<DashboardResponse> Create(DashboardRequest request, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Dashboard name is required");
        }

        var entity = new Dashboard
        {
            UserId = userId,
            Name = request.Name.Trim(),
            IsDefault = false,
            DateCreated = DateTime.UtcNow
        };

        _context.Dashboards.Add(entity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created dashboard {DashboardId} for user {UserId}", entity.Id, userId);

        return MapToResponse(entity);
    }

    public async Task Update(int id, DashboardRequest request, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Dashboard name is required");
        }

        var dashboard = await _context.Dashboards
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

        if (dashboard == null)
        {
            throw new KeyNotFoundException("Dashboard not found");
        }

        dashboard.Name = request.Name.Trim();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated dashboard {DashboardId}", id);
    }

    public async Task Delete(int id, Guid userId)
    {
        var dashboard = await _context.Dashboards
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

        if (dashboard == null)
        {
            throw new KeyNotFoundException("Dashboard not found");
        }

        _context.Dashboards.Remove(dashboard);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted dashboard {DashboardId}", id);
    }

    public async Task<IList<DashboardPanelResponse>> GetPanels(int dashboardId, Guid userId)
    {
        await EnsureDashboardAccess(dashboardId, userId);

        var panels = await _context.DashboardPanels
            .AsNoTracking()
            .Include(p => p.Tag)
                .ThenInclude(t => t!.Unit)
            .Where(p => p.DashboardId == dashboardId)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();

        return panels.Select(MapPanelToResponse).ToList();
    }

    public async Task<DashboardPanelResponse> AddPanel(int dashboardId, DashboardPanelRequest request, Guid userId)
    {
        await EnsureDashboardAccess(dashboardId, userId);
        await ValidatePanelRequest(request, userId);

        var entity = new DashboardPanel
        {
            DashboardId = dashboardId,
            PanelTypeId = request.PanelTypeId,
            TagId = request.TagId,
            AggregationTypeId = request.AggregationTypeId,
            TimeRangeId = request.TimeRangeId,
            SizeId = request.SizeId,
            DisplayOrder = request.DisplayOrder,
            Title = request.Title?.Trim(),
            IsActive = request.IsActive,
            DateCreated = DateTime.UtcNow
        };

        _context.DashboardPanels.Add(entity);
        await _context.SaveChangesAsync();

        // Reload with navigation properties
        await _context.Entry(entity).Reference(p => p.Tag).LoadAsync();
        if (entity.Tag != null)
        {
            await _context.Entry(entity.Tag).Reference(t => t.Unit).LoadAsync();
        }

        _logger.LogInformation("Added panel {PanelId} to dashboard {DashboardId}", entity.Id, dashboardId);

        return MapPanelToResponse(entity);
    }

    public async Task UpdatePanel(int dashboardId, int panelId, DashboardPanelRequest request, Guid userId)
    {
        await EnsureDashboardAccess(dashboardId, userId);
        await ValidatePanelRequest(request, userId);

        var panel = await _context.DashboardPanels
            .FirstOrDefaultAsync(p => p.Id == panelId && p.DashboardId == dashboardId);

        if (panel == null)
        {
            throw new KeyNotFoundException("Panel not found");
        }

        panel.PanelTypeId = request.PanelTypeId;
        panel.TagId = request.TagId;
        panel.AggregationTypeId = request.AggregationTypeId;
        panel.TimeRangeId = request.TimeRangeId;
        panel.SizeId = request.SizeId;
        panel.DisplayOrder = request.DisplayOrder;
        panel.Title = request.Title?.Trim();
        panel.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated panel {PanelId} on dashboard {DashboardId}", panelId, dashboardId);
    }

    public async Task RemovePanel(int dashboardId, int panelId, Guid userId)
    {
        await EnsureDashboardAccess(dashboardId, userId);

        var panel = await _context.DashboardPanels
            .FirstOrDefaultAsync(p => p.Id == panelId && p.DashboardId == dashboardId);

        if (panel == null)
        {
            throw new KeyNotFoundException("Panel not found");
        }

        _context.DashboardPanels.Remove(panel);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Removed panel {PanelId} from dashboard {DashboardId}", panelId, dashboardId);
    }

    public async Task ReorderPanels(int dashboardId, List<PanelReorderRequest> request, Guid userId)
    {
        await EnsureDashboardAccess(dashboardId, userId);

        var panels = await _context.DashboardPanels
            .Where(p => p.DashboardId == dashboardId)
            .ToListAsync();

        var orderMap = request.ToDictionary(r => r.Id, r => r.DisplayOrder);

        foreach (var panel in panels)
        {
            if (orderMap.TryGetValue(panel.Id, out var newOrder))
            {
                panel.DisplayOrder = newOrder;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Reordered panels on dashboard {DashboardId}", dashboardId);
    }

    public async Task<DashboardDataResponse> GetDashboardData(int dashboardId, Guid userId)
    {
        await EnsureDashboardAccess(dashboardId, userId);

        var panels = await _context.DashboardPanels
            .AsNoTracking()
            .Include(p => p.Tag)
                .ThenInclude(t => t!.Unit)
            .Where(p => p.DashboardId == dashboardId && p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync();

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        // Collect all tag IDs needed for batch query
        var tagIds = panels
            .Where(p => p.TagId.HasValue)
            .Select(p => p.TagId!.Value)
            .Distinct()
            .ToList();

        // Batch load latest activities for all tags at once (today only for Phase 1)
        var latestActivities = new Dictionary<int, Activity>();
        if (tagIds.Count > 0)
        {
            var todayActivities = await _context.Activities
                .AsNoTracking()
                .Where(a => a.UserId == userId
                    && tagIds.Contains(a.TagId)
                    && a.DateStarted >= today
                    && a.DateStarted < tomorrow)
                .OrderByDescending(a => a.DateStarted)
                .ToListAsync();

            // Group by tag, take latest per tag
            foreach (var group in todayActivities.GroupBy(a => a.TagId))
            {
                latestActivities[group.Key] = group.First();
            }
        }

        var panelDataList = new List<PanelDataResponse>();

        foreach (var panel in panels)
        {
            var data = new PanelDataResponse
            {
                PanelId = panel.Id,
                PanelTypeId = panel.PanelTypeId,
                TagName = panel.Tag?.TagName,
                UnitSymbol = panel.Tag?.Unit?.Symbol
            };

            if (panel.TagId.HasValue && latestActivities.TryGetValue(panel.TagId.Value, out var activity))
            {
                if (decimal.TryParse(activity.Description, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var numericValue))
                {
                    data.HasData = true;
                    data.NumericValue = numericValue;
                    data.DisplayValue = FormatDisplayValue(numericValue, panel.Tag?.Unit?.Symbol);
                }
                else if (!string.IsNullOrWhiteSpace(activity.Description))
                {
                    // Non-numeric description — show as-is
                    data.HasData = true;
                    data.DisplayValue = activity.Description;
                }
            }

            panelDataList.Add(data);
        }

        return new DashboardDataResponse
        {
            DashboardId = dashboardId,
            Panels = panelDataList
        };
    }

    private async Task<Dashboard> GetDashboardEntity(int id, Guid userId)
    {
        var dashboard = await _context.Dashboards
            .AsNoTracking()
            .Include(d => d.Panels.OrderBy(p => p.DisplayOrder))
                .ThenInclude(p => p.Tag)
                    .ThenInclude(t => t!.Unit)
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

        if (dashboard == null)
        {
            throw new KeyNotFoundException("Dashboard not found");
        }

        return dashboard;
    }

    private async Task EnsureDashboardAccess(int dashboardId, Guid userId)
    {
        var exists = await _context.Dashboards
            .AnyAsync(d => d.Id == dashboardId && d.UserId == userId);

        if (!exists)
        {
            throw new KeyNotFoundException("Dashboard not found");
        }
    }

    private async Task ValidatePanelRequest(DashboardPanelRequest request, Guid userId)
    {
        if (!ValidPanelTypeIds.Contains(request.PanelTypeId))
        {
            throw new ArgumentException($"Invalid panel type: {request.PanelTypeId}");
        }

        if (!ValidAggregationTypeIds.Contains(request.AggregationTypeId))
        {
            throw new ArgumentException($"Invalid aggregation type: {request.AggregationTypeId}");
        }

        if (!ValidTimeRangeIds.Contains(request.TimeRangeId))
        {
            throw new ArgumentException($"Invalid time range: {request.TimeRangeId}");
        }

        if (!ValidSizeIds.Contains(request.SizeId))
        {
            throw new ArgumentException($"Invalid panel size: {request.SizeId}");
        }

        if (request.PanelTypeId == DashboardPanelTypeIds.NumericValue)
        {
            if (!request.TagId.HasValue)
            {
                throw new ArgumentException("Numeric value panel requires a tag");
            }

            var tag = await _context.Tags
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == request.TagId.Value && t.UserId == userId);

            if (tag == null)
            {
                throw new KeyNotFoundException("Tag not found");
            }

            if (tag.InputTypeId.HasValue && !NumericInputTypeIds.Contains(tag.InputTypeId.Value))
            {
                throw new ArgumentException("Numeric value panel requires a tag with a numeric input type");
            }
        }
    }

    private static string FormatDisplayValue(decimal value, string? unitSymbol)
    {
        var formatted = value == Math.Floor(value)
            ? value.ToString("0")
            : value.ToString("0.##");

        if (!string.IsNullOrWhiteSpace(unitSymbol))
        {
            return $"{formatted} {unitSymbol}";
        }

        return formatted;
    }

    private static DashboardResponse MapToResponse(Dashboard dashboard)
    {
        return new DashboardResponse
        {
            Id = dashboard.Id,
            Name = dashboard.Name,
            IsDefault = dashboard.IsDefault,
            DateCreated = dashboard.DateCreated,
            Panels = dashboard.Panels.Select(MapPanelToResponse).ToList()
        };
    }

    private static DashboardPanelResponse MapPanelToResponse(DashboardPanel panel)
    {
        return new DashboardPanelResponse
        {
            Id = panel.Id,
            PanelTypeId = panel.PanelTypeId,
            TagId = panel.TagId,
            TagName = panel.Tag?.TagName,
            TagUnitSymbol = panel.Tag?.Unit?.Symbol,
            InputTypeId = panel.Tag?.InputTypeId,
            AggregationTypeId = panel.AggregationTypeId,
            TimeRangeId = panel.TimeRangeId,
            SizeId = panel.SizeId,
            DisplayOrder = panel.DisplayOrder,
            Title = panel.Title,
            IsActive = panel.IsActive,
            DateCreated = panel.DateCreated
        };
    }
}
