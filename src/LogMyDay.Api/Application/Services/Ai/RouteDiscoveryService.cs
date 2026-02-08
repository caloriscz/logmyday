using System.Reflection;
using LogMyDay.Shared.Attributes;
using LogMyDay.Shared.DTOs.Ai;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace LogMyDay.Api.Application.Services.Ai;

/// <summary>
/// Service for discovering and managing application routes for AI navigation.
/// </summary>
public interface IRouteDiscoveryService
{
    /// <summary>
    /// Gets all user-navigable routes in the application, optionally filtered by client context.
    /// </summary>
    /// <param name="context">Optional client context filter (Web, Mobile, or All). When null, returns all routes.</param>
    List<NavigableRoute> GetNavigationMap(ClientContext? context = null);
}

/// <summary>
/// Implementation of route discovery service using reflection-based dynamic discovery.
/// </summary>
public sealed class RouteDiscoveryService : IRouteDiscoveryService
{
    private readonly ILogger<RouteDiscoveryService> _logger;
    private readonly Lazy<List<NavigableRoute>> _routes;

    public RouteDiscoveryService(IEnumerable<Assembly> assemblies, ILogger<RouteDiscoveryService> logger)
    {
        _logger = logger;
        _routes = new Lazy<List<NavigableRoute>>(() => DiscoverRoutes(assemblies));
    }

    public List<NavigableRoute> GetNavigationMap(ClientContext? context = null)
    {
        var allRoutes = _routes.Value;

        if (context is null)
            return allRoutes;

        return allRoutes
            .Where(r => r.ClientContext == ClientContext.All || r.ClientContext == context.Value)
            .ToList();
    }

    private List<NavigableRoute> DiscoverRoutes(IEnumerable<Assembly> assemblies)
    {
        var routes = new List<NavigableRoute>();

        foreach (var assembly in assemblies)
        {
            try
            {
                var types = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(ComponentBase).IsAssignableFrom(t));

                foreach (var type in types)
                {
                    var routeAttribute = type.GetCustomAttribute<RouteAttribute>();
                    var aiAttribute = type.GetCustomAttribute<AiNavigableRouteAttribute>();

                    if (routeAttribute is null || aiAttribute is null)
                        continue;

                    var routePath = routeAttribute.Template;

                    // Skip parameterized routes (e.g., /tags/edit/{Id:int})
                    if (routePath.Contains('{'))
                    {
                        _logger.LogDebug("Skipping parameterized route: {Route}", routePath);
                        continue;
                    }

                    routes.Add(new NavigableRoute(
                        routePath,
                        aiAttribute.Label,
                        aiAttribute.Description,
                        aiAttribute.RequiresAdmin,
                        aiAttribute.ClientContext
                    ));

                    _logger.LogInformation("Discovered AI-navigable route: {Path} ({Label})", routePath, aiAttribute.Label);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scan assembly {Assembly} for routes", assembly.FullName);
            }
        }

        _logger.LogInformation("Route discovery complete: {Count} routes found", routes.Count);
        return routes;
    }
}
